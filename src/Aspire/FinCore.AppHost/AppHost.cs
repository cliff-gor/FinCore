var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure (spec 001: Postgres 17, Keycloak, Seq only in M0).
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("17")
    .WithInitFiles("../../../deploy/postgres")
    // Named volumes keep data between `dotnet run` sessions; remove them to start from an empty database.
    .WithDataVolume("fincore-postgres-data");
var database = postgres.AddDatabase("fincore");

var keycloak = builder.AddKeycloak("keycloak", port: 8080)
    .WithRealmImport("../../../deploy/keycloak")
    .WithDataVolume("fincore-keycloak-data");

var seq = builder.AddSeq("seq", port: 5341);

// API (composition root of all modules).
var api = builder.AddProject<Projects.FinCore_Api>("api", launchProfileName: "https")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Seq__ServerUrl", seq.GetEndpoint("http"))
    // The API only validates tokens, so it needs the realm URL but no client secret.
    .WithEnvironment("Auth__Authority", ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/fincore"))
    .WaitFor(keycloak);

// Gateway in its three modes: BackOffice BFF, Portal BFF, Mobile Gateway.
IResourceBuilder<ProjectResource> Gateway(string name, string profile) =>
    builder.AddProject<Projects.FinCore_Bff>(name, launchProfileName: profile)
        // Point YARP at whatever port Aspire assigned the API, instead of the fixed port in appsettings.json.
        .WithEnvironment("ReverseProxy__Clusters__api__Destinations__d1__Address", api.GetEndpoint("http"))
        .WithReference(api)
        .WaitFor(api);

var backOfficeBff = Gateway("bff-backoffice", "BackOffice");
var portalBff = Gateway("bff-portal", "Portal");
Gateway("gateway-mobile", "Mobile");

// Web apps (Vite dev servers). The Expo dev server is started separately: pnpm --filter mobile start.
builder.AddViteApp("backoffice", "../../../frontend/apps/backoffice")
    .WithPnpm()
    .WithReference(backOfficeBff)
    .WaitFor(backOfficeBff);

builder.AddViteApp("portal", "../../../frontend/apps/portal")
    .WithPnpm()
    .WithReference(portalBff)
    .WaitFor(portalBff);

builder.Build().Run();
