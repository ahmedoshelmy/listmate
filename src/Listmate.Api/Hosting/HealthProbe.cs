namespace Listmate.Api.Hosting;

/// <summary>
/// `dotnet Listmate.Api.dll --probe http://localhost:8080/health/ready` exits 0 when healthy, so compose can
/// health-check the slim ASP.NET runtime image, which ships without curl or wget.
/// </summary>
internal static class HealthProbe
{
    public static async Task RunIfRequestedAsync(string[] args)
    {
        if (args is not ["--probe", var url])
        {
            return;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await http.GetAsync(new Uri(url));
            Environment.Exit(response.IsSuccessStatusCode ? 0 : 1);
        }
        catch (HttpRequestException)
        {
            Environment.Exit(1);
        }
        catch (TaskCanceledException)
        {
            Environment.Exit(1);
        }
    }
}
