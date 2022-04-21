Store the keys required by .NET Data Protection library in your PostgreSQL databae.

How to configure:

```
using DataProtection.PostgreSQL;

builder.Services.AddDataProtection().ProtectKeysWithCertificate(X509Repository.GetCertificate(connectionString));
builder.Services.Configure<KeyManagementOptions>(options => options.XmlRepository = new DataProtection.PostgreSQL.DataProtectionRepository(connectionString));
```

Available on Nuget as **PPioli.DataProtection.PostgreSQL**