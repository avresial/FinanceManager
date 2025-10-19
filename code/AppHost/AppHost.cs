var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.FinanceManager_Api>("api");
//var web = builder.AddProject<Projects.FinanceManager_WebUi>("web");

builder.Build().Run();