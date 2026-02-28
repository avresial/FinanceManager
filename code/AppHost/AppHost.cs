var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgreSQLServer")
                            .WithPgAdmin()
                            .WithLifetime(ContainerLifetime.Persistent)
                            .WithDataBindMount(source: @"C:\Users\Miki\Documents\Repositories\Docker");

var db = postgresServer.AddDatabase("FinanceManagerDb");

builder.AddProject<Projects.FinanceManager_Api>("api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();