namespace PodiumdAdapter.Web.Infrastructure
{
    public static class Helpers
    {
        public static string GetRequiredValue(this IConfiguration configuration, string key)
        {
            var result = configuration[key];
            if (string.IsNullOrWhiteSpace(result)) throw new Exception("configuratie niet gevonden: " + key);
            return result;
        }
    }
}
