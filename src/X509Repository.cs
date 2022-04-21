using Microsoft.Extensions.Logging;
using Npgsql;
using Postgres.SchemaUpdater;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DataProtection.PostgreSQL
{
    public static class X509Repository
    {
        const string _schemaName = "dataprotection";
        const string _tableName = "certs";
        const string _fieldName = "content";

        public static X509Certificate2 GetCertificate(string connectionString, ILogger? logger = null)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var tableExists = DdlTools.DoesTableExists(connection, _schemaName, _tableName);

            if (!tableExists)
            {
                CreateTable(connection, logger);
            }

            using var queryCommand = connection.CreateCommand();
            queryCommand.CommandText = $"select {_fieldName} from {_schemaName}.{_tableName}";
            using var reader = queryCommand.ExecuteReader();

            var cert = reader.ToList(x => new X509Certificate2(Convert.FromBase64String(x.GetString(0)))).FirstOrDefault();
            connection.Close();

            if (cert == null)
            {
                var newCert = CreateNewCertificate();
                var serializedCert = Convert.ToBase64String(newCert);

                connection.Open();
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = $"insert into {_schemaName}.{_tableName}(id, content) values (@id, @content)";
                insertCommand.Parameters.AddWithValue("@id", Guid.NewGuid());
                insertCommand.Parameters.AddWithValue("@content", serializedCert);
                insertCommand.ExecuteNonQuery();

                cert = new X509Certificate2(newCert);
            }

            return cert;
        }

        private static void CreateTable(NpgsqlConnection connection, ILogger? logger)
        {
            var columns = new List<Column>()
            {
                new Column("id", "uuid", nullable: false),
                new Column(_fieldName, "text", nullable: false)
            };

            var table = new Table(_schemaName, _tableName, columns)
            {
                PrimaryKey = columns[0]
            };

            var catalog = new Catalog(table);

            var scripts = DdlTools.GenerateUpgradeScripts(catalog, connection);

            foreach (var script in scripts)
            {
                logger?.LogInformation(script);

                var command = connection.CreateCommand();
                command.CommandText = script;
                command.ExecuteNonQuery();
            }
        }

        private static byte[] CreateNewCertificate()
        {
            var distinguishedName = new X500DistinguishedName("CN=dataprotection");

            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

            var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(36500)));

            return certificate.Export(X509ContentType.Pfx);
        }
    }
}
