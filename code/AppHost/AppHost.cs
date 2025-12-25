var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgreSQLServer")
                            .WithDataBindMount(source: @"C:\Users\Miki\Documents\Repositories\Docker");

var exampleDatabase = postgresServer.AddDatabase("testDB");

builder.AddProject<Projects.FinanceManager_Api>("api")
    .WithReference(exampleDatabase)
    .WaitFor(exampleDatabase);

builder.Build().Run();