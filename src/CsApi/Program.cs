namespace Api;

public static class Program {
    public static void Main(string[] args) {
        var builder =
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());

        builder.Build().Run();
    }
}
