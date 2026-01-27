var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure (spec 001: Postgres 17, Keycloak, Seq only in M0).
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("17")
    .WithInitFiles("../../../deploy/postgres")
    // Named volumes keep data between `dotnet run` sessions; remove them to start from an empty database.
    .WithDataVolume("fincore-postgres-data");
var database = postgres.AddDatabase("fincore");

var keycloak = builder.AddKeycloak("keycloak", port: 8080)
    // Imports both realm files (fincore-staff, fincore-customers; spec 002). Dev users: docs/dev-accounts.md.
    .WithRealmImport("../../../deploy/keycloak")
    .WithDataVolume("fincore-keycloak-data");

var seq = builder.AddSeq("seq", port: 5341);

// API (composition root of all modules).
var api = builder.AddProject<Projects.FinCore_Api>("api", launchProfileName: "https")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Seq__ServerUrl", seq.GetEndpoint("http"))
    // The API only validates tokens, so it needs the two realm URLs (staff and customers, spec 002) but no secrets.
    .WithEnvironment("Auth__Staff__Authority", ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/fincore-staff"))
    .WithEnvironment("Auth__Customers__Authority", ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/fincore-customers"))
    .WaitFor(keycloak);

// Gateway in its three modes: BackOffice BFF, Portal BFF, Mobile Gateway.
IResourceBuilder<ProjectResource> Gateway(string name, string profile) =>
    builder.AddProject<Projects.FinCore_Bff>(name, launchProfileName: profile)
        // Point YARP at whatever port Aspire assigned the API, instead of the fixed port in appsettings.json.
        .WithEnvironment("ReverseProxy__Clusters__api__Destinations__d1__Address", api.GetEndpoint("http"))
        .WithReference(api)
        .WaitFor(api);

// The two web BFFs sign in with OIDC against their own realm (client secret: launchSettings.json, dev only) and keep
// sessions and Data Protection keys in the platform schema, so they need the database too (spec 002).
IResourceBuilder<ProjectResource> WebBff(string name, string profile, string realm) =>
    Gateway(name, profile)
        .WithReference(database)
        .WithEnvironment("Bff__Oidc__Authority", ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/{realm}"));

var backOfficeBff = WebBff("bff-backoffice", "BackOffice", "fincore-staff");
var portalBff = WebBff("bff-portal", "Portal", "fincore-customers");
Gateway("gateway-mobile", "Mobile");

// Web apps (Vite dev servers). The Expo dev server is started separately: pnpm --filter mobile start.
var backOffice = builder.AddViteApp("backoffice", "../../../frontend/apps/backoffice")
    .WithPnpm()
    .WithReference(backOfficeBff)
    .WaitFor(backOfficeBff);

var portal = builder.AddViteApp("portal", "../../../frontend/apps/portal")
    .WithPnpm()
    .WithReference(portalBff)
    .WaitFor(portalBff);

// Each BFF serves its app's dev server on its own origin (https://localhost:7200 / :7201), because the OIDC callback
// and the post-sign-in redirect land there. Open the apps through the BFF URLs to sign in.
backOfficeBff.WithEnvironment("Bff__DevSpaUrl", backOffice.GetEndpoint("http"));
portalBff.WithEnvironment("Bff__DevSpaUrl", portal.GetEndpoint("http"));

builder.Build().Run();
