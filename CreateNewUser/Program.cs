using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.IO;
class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("There are no arguments in the call");

            return;
        }

        try
        {
            switch (args[0])
            {
                case "auth":
                    Authorization(args[1], args[2], args[3]).Wait();
                    break;
                case "newuser":
                    CreateNewUser(args[1], args[2], args[3]).Wait();
                    break;
                case "addip":
                    AddIp(args[1]);
                    break;
                default:
                    Console.WriteLine("This method is missing");
                    break;
            }
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException.GetType() == typeof(FileNotFoundException))
                Console.WriteLine("You are not authorized");
            else
                Console.WriteLine(ex.Message);
        }
        catch (IndexOutOfRangeException ex)
        {
            Console.WriteLine("Wrong number of arguments");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public static void AddIp(string ip)
    {
        StreamWriter sr = new StreamWriter("current_ip");
        sr.WriteLine(ip);
        sr.Close();
        Console.WriteLine("Ip address added successfully");
    }

    public static async Task Authorization(string username, string password, string ip)
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (requestMessage, certificate, chain, policyErrors) => true;
        using var httpClient = new HttpClient(handler);

        AddIp(ip);

        using var request = new HttpRequestMessage(new HttpMethod("POST"), $"https://{ip}/api/auth");
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.ceph.api.v1.0+json");

        request.Content = new StringContent($"{{\"username\": \"{username}\", \"password\": \"{password}\"}}");
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        var response = await httpClient.SendAsync(request);

        string json = await response.Content.ReadAsStringAsync();
        Token? token = JsonSerializer.Deserialize<Token>(json);

        FileStreamOptions options = new FileStreamOptions();
        options.Mode = FileMode.CreateNew;
        StreamWriter sw = new StreamWriter("token");
        sw.WriteLineAsync(token.token);
        sw.Close();

        if (token.token != null || token.token!="")
        {
            Console.WriteLine("Token Created!");
        }
    }


    public static async Task CreateNewUser(string tenant, string name, string quota)
    {
        User user;
        var handler = new HttpClientHandler();
        handler.UseCookies = false;
        handler.ServerCertificateCustomValidationCallback = (requestMessage, certificate, chain, policyErrors) => true;

        StreamReader sr;
        string ip;
        try
        {
            sr = new StreamReader("current_ip");
            ip = sr.ReadLine();
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine("Add Ip address!!");
            return;
        }

        sr = new StreamReader("token");
        string token = sr.ReadLine();
        using (var httpClient = new HttpClient(handler))
        {
            using (var request = new HttpRequestMessage(new HttpMethod("POST"), $"https://{ip}/api/rgw/user"))
            {
                request.Headers.TryAddWithoutValidation("Accept", "application/vnd.ceph.api.v1.0+json");
                request.Headers.TryAddWithoutValidation("Cookie", "cookefile");                
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                
                request.Content = new StringContent($"{{\"display_name\": \"{name}\", \"uid\": \"{tenant}${tenant}\"}}");
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                
                var response = await httpClient.SendAsync(request);
                
                string json = await response.Content.ReadAsStringAsync();
                
                Status status = JsonSerializer.Deserialize<Status>(json);
                if (status.status!="" && status.status != null)
                {
                    Console.WriteLine(status.status);
                    Console.WriteLine(status.detail);
                    return;
                }
                if (status.detail != "" && status.detail != null)
                {
                    Console.WriteLine(status.detail);
                    return;
                }
                user = JsonSerializer.Deserialize<User>(json);

                Console.WriteLine("New User Created!");
                Console.WriteLine($"Access Key - {user.keys[0].access_key}");
                Console.WriteLine($"Secret Key - {user.keys[0].secret_key}");
            }
        }


        /*handler = new HttpClientHandler();
        handler.UseCookies = false;
        handler.ServerCertificateCustomValidationCallback = (requestMessage, certificate, chain, policyErrors) => true;

        using (var httpClient = new HttpClient(handler))
        {
            using (var request = new HttpRequestMessage(new HttpMethod("POST"), "https://ceph-test02:8443/api/rgw/tesescripttenant$tesescripttenant/quota"))
            {
                request.Headers.TryAddWithoutValidation("Accept", "application/vnd.ceph.api.v1.0+json");
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJjZXBoLWRhc2hib2FyZCIsImp0aSI6IjI0NDNmMWIyLTBlOTUtNDEwYS1iOWQ4LTBkY2Q4NjRmN2QwZiIsImV4cCI6MTY4MDAyMTYxMCwiaWF0IjoxNjc5OTkyODEwLCJ1c2VybmFtZSI6ImFkbWluIn0.SCqritwi_VjMslAE-Gj87IU2uAO4R-ybZDnuAJtqvmU");
                request.Headers.TryAddWithoutValidation("Cookie", "-X");

                request.Content = new StringContent("{\"daemon_name\": \"ceph-test02\", \"enabled\": \"true\", \"max_objects\": \"ulimited\", \"max_size_kb\": 1024,  \"quota_type\": \"user\"}");
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

                var response = await httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                Console.WriteLine(json);
            }
        }*/
    }

    static async Task SetQuota(int quota)
    {
        
    }

    class Token
    {
        public string token { get; }

        public Token (string token) { this.token = token; }
    }
    
    class Status
    {
        public string status { get; }
        public string detail { get; }

        public Status (string status, string detail)
        {
            this.status = status; this.detail = detail;
        }
    }

    class User
    {
        public string tenant { get; }
        public Key[] keys { get; }

        public string uid { get; }
        public User (string tenant, Key[] keys, string uid)
        {
            this.tenant = tenant;
            this.keys = keys;
            this.uid = uid;
        }
    }

    class Key
    {
        public string access_key { get; }
        public string secret_key { get; }

        public Key (string access_key, string secret_key)
        {
            this.access_key = access_key;
            this.secret_key = secret_key;
        }
    }

    class UncreatedUser
    {
        public string display_name { get; }
        public string uid { get; }

        public UncreatedUser(string display_name, string uid)
        {
            this.display_name = display_name;
            this.uid = uid;
        }
    }
}