#:package Microsoft.Extensions.Configuration.UserSecrets@10.0.0

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

var key = args.Length > 0 ? args[0] : "MySecret";

var configuration = new ConfigurationBuilder()
    .AddUserSecrets()
    .Build();

Console.WriteLine(configuration[key] ?? $"""
    Value for secret '{key}' was not found. Run the following command to set one:
    dotnet user-secrets set {key} "This is a secret!" --file {Path.GetFileName((string)AppContext.GetData("EntryPointFilePath")!)}
    """);

static class UserSecretsExtensions
{
    extension(IConfigurationBuilder builder)
    {
        /// <summary>
        /// Adds user secrets configuration source to <see cref="IConfigurationBuilder"/> based on the user secrets ID returned by <see cref="GetDefaultUserSecretsId"/>.
        /// </summary>
        public IConfigurationBuilder AddUserSecrets()
        {
            return builder.AddUserSecrets(GetDefaultUserSecretsId());
        }

        private static string GetDefaultUserSecretsId()
        {
            var userSecretsId = Assembly.GetEntryAssembly()?.GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId;
            if (userSecretsId is not null)
            {
                // Use the UserSecretsId from the attribute
                return userSecretsId;
            }

            return "global";
        }
    }
}
