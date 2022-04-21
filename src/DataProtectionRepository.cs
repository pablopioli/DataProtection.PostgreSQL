using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;
using Postgres.SchemaUpdater;
using System.Xml.Linq;

namespace DataProtection.PostgreSQL;

public class DataProtectionRepository : IXmlRepository
{
    const string _schemaName = "dataprotection";
    const string _tableName = "keys";
    const string _fieldName = "content";

    private readonly string _connectionString;

    public DataProtectionRepository(string connectionString, ILogger? logger = null)
    {
        _connectionString = connectionString;

        using var connection = new Npgsql.NpgsqlConnection(connectionString);
        connection.Open();

        var tableExists = DdlTools.DoesTableExists(connection, _schemaName, _tableName);

        if (!tableExists)
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
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = $"select {_fieldName} from {_schemaName}.{_tableName}";
        using var reader = command.ExecuteReader();

        return reader.ToList(x => XElement.Parse(x.GetString(0))).ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = $"insert into {_schemaName}.{_tableName}(id, content) values (@id, @content)";
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@content", element.ToString());
        command.ExecuteNonQuery();
    }
}
