IConfigurationRoot config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();
int port = config.GetValue<int?>("Port") ?? 80;
string contentDirectory = config.GetValue<string>("Directory") ?? Directory.GetCurrentDirectory();

TcpListener listener = new TcpListener(IPAddress.Any, port);
listener.Start();
Console.WriteLine($"Server is listening on port {port}...");

var clientsCounter = 0;
while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    var clientInfo = new ClientInfo(clientsCounter++, DateTime.Now);
    Console.WriteLine($"Client connected! {clientInfo}");
    new Thread(() => _ = HandleClientAsync(client, clientInfo)).Start();
}

async Task HandleClientAsync(TcpClient client, ClientInfo clientInfo)
{
    await using NetworkStream stream = client.GetStream();
    byte[] buffer = new byte[1024];
    int bytesRead;
    string? path = null;
    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
    {
        string receivedText = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        if (path == null)
        {
            var parts = receivedText.Split(' ');
            if (parts.Length > 1)
            {
                path = parts[1];
                break;
            }
        }
    }

    var htmlContent = GetHtmlContent(path);
    
    var responseString = htmlContent != null ? GetResponse(HttpStatusCode.OK, htmlContent) : GetResponse(HttpStatusCode.NotFound);
    byte[] response = Encoding.UTF8.GetBytes(responseString);
    //Thread.Sleep(5_000);
    await stream.WriteAsync(response, 0, response.Length);
    client.Close();
    Console.WriteLine($"Client disconnected. {(DateTime.Now - clientInfo.Connected).TotalMilliseconds:0000.00}. {clientInfo}. Path: {path}" );
}

string? GetHtmlContent(string? path)
{
    if (string.IsNullOrEmpty(path) || path == "/")
        path = "/index.html";
    var filePath = Path.Combine(contentDirectory, path.Replace('/', Path.DirectorySeparatorChar)[1..]);
    if (!filePath.Contains(contentDirectory)) // alternative way to check - recursive compare parent folder with contentDir 
        return null;
    if (File.Exists(filePath))
        return File.ReadAllText(filePath);
    
    return null;
}

static string GetResponse(HttpStatusCode statusCode, string? content = null)
{
    if ((int)statusCode < 300 && !string.IsNullOrEmpty(content))
        return $"""
                {GetHeader(statusCode)}
                {GetContent(content)}
                """;
    return GetHeader(statusCode);
}

static string GetHeader(HttpStatusCode statusCode) => $"""
                                                       HTTP/1.1 {(int)statusCode} {statusCode.ToString()}

                                                       """;

static string GetContent(string content) => $"""
                                             {content}

                                             """;
                                             
public record ClientInfo(int Id, DateTime Connected);