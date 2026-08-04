var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgreSQLServer")
                            .WithImageTag("17")
                            .WithPgAdmin()
                            .WithLifetime(ContainerLifetime.Persistent)
                            .WithDataVolume();

var db = postgresServer.AddDatabase("FinanceManagerDb");

builder.AddProject<Projects.FinanceManager_Api>("api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();