using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Options;
using MyDocuMgm.Application.ExternalFetch;
using MyDocuMgm.Domain;
using MyDocuMgm.Infrastructure.ExternalFetch;

namespace MyDocuMgm.UnitTests;

public sealed class ExternalFetchTests
{
    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("2001:db8::1", false)]
    public void PublicAddressPolicy_BlocksSpecialRanges(string value, bool expected)
    {
        Assert.Equal(expected, SsrfSafeDestinationValidator.IsPublic(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("http://example.test:8080/path", "FETCH_PORT_NOT_ALLOWED")]
    [InlineData("http://127.0.0.1/path", "FETCH_IP_LITERAL_NOT_ALLOWED")]
    [InlineData("file:///c:/private.txt", "FETCH_URL_NOT_ALLOWED")]
    public async Task DestinationPolicy_RejectsDisallowedUrlForms(string value, string code)
    {
        var validator = new SsrfSafeDestinationValidator(
            new DnsResolver(IPAddress.Parse("8.8.8.8")));

        var error = await Assert.ThrowsAsync<ExternalFetchException>(
            () => validator.ValidateAsync(new Uri(value), default));

        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task DestinationPolicy_RejectsMixedPublicAndPrivateDnsAnswers()
    {
        var validator = new SsrfSafeDestinationValidator(new DnsResolver(
            IPAddress.Parse("8.8.8.8"),
            IPAddress.Parse("10.0.0.2")));

        var error = await Assert.ThrowsAsync<ExternalFetchException>(
            () => validator.ValidateAsync(new Uri("https://example.test/path"), default));

        Assert.Equal("FETCH_DNS_NOT_PUBLIC", error.Code);
    }

    [Fact]
    public void RobotsPolicy_UsesLongestMatchAndAllowWinsTie()
    {
        const string robots = """
            User-agent: *
            Disallow: /private
            Allow: /private/public
            """;
        var policy = new RobotsPolicyEvaluator();

        Assert.False(policy.IsAllowed(robots, new Uri("https://example.test/private/item")));
        Assert.True(policy.IsAllowed(robots, new Uri("https://example.test/private/public/item")));
        Assert.True(policy.IsAllowed(robots, new Uri("https://example.test/open")));
    }

    [Fact]
    public void RobotsPolicy_UsesTheActualProductTokenInsteadOfWildcardFallback()
    {
        const string robots = """
            User-agent: MyDocuMgmPhase2C
            Disallow: /private
            User-agent: *
            Allow: /
            """;

        var policy = new RobotsPolicyEvaluator();

        Assert.False(policy.IsAllowed(robots, new Uri("https://example.test/private")));
    }

    [Fact]
    public void RobotsPolicy_MergesCaseInsensitiveProductGroups()
    {
        const string robots = """
            User-agent: mydocumgmphase2c
            Disallow: /private
            User-agent: MYDOCUMGMPHASE2C
            Allow: /private/public
            User-agent: *
            Allow: /
            """;

        var policy = new RobotsPolicyEvaluator();

        Assert.False(policy.IsAllowed(robots, new Uri("https://example.test/private/item")));
        Assert.True(policy.IsAllowed(robots, new Uri("https://example.test/private/public/item")));
    }

    [Fact]
    public async Task Fetcher_ReevaluatesCachedRobotsPolicyForSameAuthorityRedirect()
    {
        var handler = new RoutingHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => Text(HttpStatusCode.OK, "User-agent: *\nDisallow: /private", "text/plain"),
            "/open" => Redirect("/private"),
            "/private" => Text(HttpStatusCode.OK, "<main><p>must not be requested</p></main>", "text/html"),
            _ => Text(HttpStatusCode.NotFound, string.Empty, "text/plain")
        });
        var fetcher = CreateFetcher(handler);

        var error = await Assert.ThrowsAsync<ExternalFetchException>(
            () => fetcher.FetchAsync(new Uri("https://example.test/open"), default));

        Assert.Equal("FETCH_ROBOTS_DISALLOWED", error.Code);
        Assert.DoesNotContain(handler.Requests, uri => uri.AbsolutePath == "/private");
        Assert.Single(handler.Requests, uri => uri.AbsolutePath == "/robots.txt");
    }

    [Fact]
    public async Task Fetcher_FollowsValidatedRobotsRedirectAndAppliesFinalPolicy()
    {
        var handler = new RoutingHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => Redirect("/robots-policy"),
            "/robots-policy" => Text(HttpStatusCode.OK, "User-agent: *\nDisallow: /private", "text/plain"),
            "/private" => Text(HttpStatusCode.OK, "<main><p>must not be requested</p></main>", "text/html"),
            _ => Text(HttpStatusCode.NotFound, string.Empty, "text/plain")
        });
        var fetcher = CreateFetcher(handler);

        var error = await Assert.ThrowsAsync<ExternalFetchException>(
            () => fetcher.FetchAsync(new Uri("https://example.test/private"), default));

        Assert.Equal("FETCH_ROBOTS_DISALLOWED", error.Code);
        Assert.DoesNotContain(handler.Requests, uri => uri.AbsolutePath == "/private");
        Assert.Contains(handler.Requests, uri => uri.AbsolutePath == "/robots-policy");
    }

    [Fact]
    public async Task Fetcher_ReusesPolicyButEvaluatesAllowedRedirectPath()
    {
        var handler = new RoutingHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => Text(HttpStatusCode.OK, "User-agent: *\nDisallow: /private", "text/plain"),
            "/open" => Redirect("/allowed"),
            "/allowed" => Text(HttpStatusCode.OK, "<main><p>allowed body</p></main>", "text/html"),
            _ => Text(HttpStatusCode.NotFound, string.Empty, "text/plain")
        });

        var result = await CreateFetcher(handler).FetchAsync(
            new Uri("https://example.test/open"),
            default);

        Assert.Equal("https://example.test/allowed", result.FinalUrl);
        Assert.Single(handler.Requests, uri => uri.AbsolutePath == "/robots.txt");
        Assert.Contains(handler.Requests, uri => uri.AbsolutePath == "/allowed");
    }

    [Fact]
    public async Task Fetcher_EvaluatesDestinationAuthorityRobotsBeforeRedirectedPage()
    {
        var handler = new RoutingHandler(request => (request.RequestUri!.Host, request.RequestUri.AbsolutePath) switch
        {
            ("example.test", "/robots.txt") => Text(HttpStatusCode.OK, "User-agent: *\nAllow: /", "text/plain"),
            ("example.test", "/open") => AbsoluteRedirect("https://other.test/private"),
            ("other.test", "/robots.txt") => Text(HttpStatusCode.OK, "User-agent: *\nDisallow: /private", "text/plain"),
            ("other.test", "/private") => Text(HttpStatusCode.OK, "<main>must not be requested</main>", "text/html"),
            _ => Text(HttpStatusCode.NotFound, string.Empty, "text/plain")
        });

        var error = await Assert.ThrowsAsync<ExternalFetchException>(() =>
            CreateFetcher(handler).FetchAsync(new Uri("https://example.test/open"), default));

        Assert.Equal("FETCH_ROBOTS_DISALLOWED", error.Code);
        Assert.Contains(handler.Requests, uri => uri.Host == "other.test" && uri.AbsolutePath == "/robots.txt");
        Assert.DoesNotContain(handler.Requests, uri => uri.Host == "other.test" && uri.AbsolutePath == "/private");
    }

    [Fact]
    public async Task Fetcher_DoesNotRequestPrivateRobotsRedirectDestination()
    {
        var handler = new RoutingHandler(request => request.RequestUri!.Host switch
        {
            "example.test" when request.RequestUri.AbsolutePath == "/robots.txt" =>
                AbsoluteRedirect("https://private.test/robots.txt"),
            "example.test" => Text(HttpStatusCode.OK, "<main><p>public body</p></main>", "text/html"),
            _ => Text(HttpStatusCode.OK, "User-agent: *\nDisallow: /", "text/plain")
        });
        var resolver = new MapDnsResolver(new Dictionary<string, IPAddress[]>
        {
            ["example.test"] = [IPAddress.Parse("8.8.8.8")],
            ["private.test"] = [IPAddress.Parse("10.0.0.1")]
        });

        var result = await CreateFetcher(handler, resolver).FetchAsync(
            new Uri("https://example.test/open"),
            default);

        Assert.Equal(200, result.HttpStatusCode);
        Assert.DoesNotContain(handler.Requests, uri => uri.Host == "private.test");
    }

    [Fact]
    public async Task Fetcher_StopsAfterThreeRobotsRedirects()
    {
        var handler = new RoutingHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => Redirect("/robots-1"),
            "/robots-1" => Redirect("/robots-2"),
            "/robots-2" => Redirect("/robots-3"),
            "/robots-3" => Redirect("/robots-4"),
            "/robots-4" => Text(HttpStatusCode.OK, "User-agent: *\nDisallow: /open", "text/plain"),
            "/open" => Text(HttpStatusCode.OK, "<main><p>public body</p></main>", "text/html"),
            _ => Text(HttpStatusCode.NotFound, string.Empty, "text/plain")
        });

        var result = await CreateFetcher(handler).FetchAsync(new Uri("https://example.test/open"), default);

        Assert.Equal(200, result.HttpStatusCode);
        Assert.DoesNotContain(handler.Requests, uri => uri.AbsolutePath == "/robots-4");
    }

    [Fact]
    public async Task Fetcher_RejectsNonHtmlBeforeReadingBody()
    {
        var content = new TrackingContent("not html", "application/json");
        var handler = new RoutingHandler(request => request.RequestUri!.AbsolutePath == "/robots.txt"
            ? Text(HttpStatusCode.NotFound, string.Empty, "text/plain")
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = content });

        var error = await Assert.ThrowsAsync<ExternalFetchException>(() =>
            CreateFetcher(handler).FetchAsync(new Uri("https://example.test/data"), default));

        Assert.Equal("FETCH_CONTENT_TYPE_UNSUPPORTED", error.Code);
        Assert.False(content.WasRead);
    }

    [Fact]
    public async Task Fetcher_BoundsChunkedBodyAndDisposesStream()
    {
        var content = new TrackingContent(new string('x', 257), "text/html");
        var handler = new RoutingHandler(request => request.RequestUri!.AbsolutePath == "/robots.txt"
            ? Text(HttpStatusCode.NotFound, string.Empty, "text/plain")
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        var options = new ExternalFetchOptions { MaxHtmlBytes = 256 };

        var error = await Assert.ThrowsAsync<ExternalFetchException>(() =>
            CreateFetcher(handler, options: options).FetchAsync(new Uri("https://example.test/large"), default));

        Assert.Equal("FETCH_RESPONSE_TOO_LARGE", error.Code);
        Assert.True(content.StreamDisposed);
    }

    [Fact]
    public async Task Fetcher_UsesConfiguredTotalTimeout()
    {
        var handler = new AsyncRoutingHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/robots.txt")
            {
                return Text(HttpStatusCode.NotFound, string.Empty, "text/plain");
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Text(HttpStatusCode.OK, string.Empty, "text/html");
        });
        var options = new ExternalFetchOptions { TotalTimeoutSeconds = 1 };

        var error = await Assert.ThrowsAsync<ExternalFetchException>(() =>
            CreateFetcher(handler, options: options).FetchAsync(new Uri("https://example.test/slow"), default));

        Assert.Equal("FETCH_TIMEOUT", error.Code);
    }

    [Fact]
    public async Task Fetcher_ConnectsOnlyToValidatedPinnedEndpointAndPreservesHost()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var localEndpoint = (IPEndPoint)listener.LocalEndpoint;
        var requests = new List<string>();
        using var serverTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var server = Task.Run(async () =>
        {
            for (var index = 0; index < 2; index++)
            {
                using var accepted = await listener.AcceptTcpClientAsync(serverTimeout.Token);
                await using var stream = accepted.GetStream();
                var buffer = new byte[4096];
                var count = await stream.ReadAsync(buffer, serverTimeout.Token);
                var requestText = Encoding.ASCII.GetString(buffer, 0, count);
                requests.Add(requestText);
                var isRobots = requestText.StartsWith("GET /robots.txt ", StringComparison.Ordinal);
                var body = isRobots ? string.Empty : "<main><p>pinned body</p></main>";
                var status = isRobots ? "404 Not Found" : "200 OK";
                var contentType = isRobots ? "text/plain" : "text/html";
                var response = Encoding.UTF8.GetBytes(
                    $"HTTP/1.1 {status}\r\nContent-Type: {contentType}; charset=utf-8\r\n" +
                    $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
                await stream.WriteAsync(response, serverTimeout.Token);
            }
        }, serverTimeout.Token);
        var resolver = new CountingDnsResolver(IPAddress.Parse("8.8.8.8"));
        var pinnedEndpoints = new List<IPEndPoint>();
        var connectionHosts = new List<string>();
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = (context, cancellationToken) =>
            {
                connectionHosts.Add(context.DnsEndPoint.Host);
                return ExternalHttpPageFetcher.ConnectPinnedAsync(
                    context,
                    cancellationToken,
                    async (pinnedEndpoint, token) =>
                    {
                        pinnedEndpoints.Add(pinnedEndpoint);
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        try
                        {
                            await socket.ConnectAsync(localEndpoint, token);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    });
            }
        };

        try
        {
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            var fetcher = new ExternalHttpPageFetcher(
                client,
                new SsrfSafeDestinationValidator(resolver),
                new RobotsPolicyEvaluator(),
                Options.Create(new ExternalFetchOptions()));

            var result = await fetcher.FetchAsync(new Uri("http://example.test/article"), default);
            await server;

            Assert.Equal(200, result.HttpStatusCode);
            Assert.Equal(2, resolver.CallCount);
            Assert.All(pinnedEndpoints, endpoint =>
            {
                Assert.Equal(IPAddress.Parse("8.8.8.8"), endpoint.Address);
                Assert.Equal(80, endpoint.Port);
            });
            Assert.All(connectionHosts, host => Assert.Equal("example.test", host));
            Assert.All(requests, request => Assert.Contains("Host: example.test", request));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Fetcher_HttpsPinningPreservesOriginalTlsHostnameValidation()
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=MyDocuMgm Phase2C Test Root",
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        using var root = rootRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1));
        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest(
            "CN=example.test",
            serverKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("example.test");
        serverRequest.CertificateExtensions.Add(san.Build());
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            true));
        serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") },
            true));
        var serial = RandomNumberGenerator.GetBytes(16);
        using var issued = serverRequest.Create(
            root,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(30),
            serial);
        using var ephemeralServerCertificate = issued.CopyWithPrivateKey(serverKey);
        using var serverCertificate = X509CertificateLoader.LoadPkcs12(
            ephemeralServerCertificate.Export(X509ContentType.Pkcs12),
            password: null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var localEndpoint = (IPEndPoint)listener.LocalEndpoint;
        using var serverTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = Task.Run(async () =>
        {
            var connections = new List<Task>();
            for (var index = 0; index < 2; index++)
            {
                var accepted = await listener.AcceptTcpClientAsync(serverTimeout.Token);
                connections.Add(Task.Run(async () =>
                {
                    using (accepted)
                    await using (var network = accepted.GetStream())
                    using (var tls = new SslStream(network, leaveInnerStreamOpen: true))
                    {
                        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                        {
                            ServerCertificate = serverCertificate,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                        }, serverTimeout.Token);
                        var buffer = new byte[4096];
                        var count = await tls.ReadAsync(buffer, serverTimeout.Token);
                        var requestText = Encoding.ASCII.GetString(buffer, 0, count);
                        var isRobots = requestText.StartsWith("GET /robots.txt ", StringComparison.Ordinal);
                        var body = isRobots ? string.Empty : "<main><p>tls pinned body</p></main>";
                        var status = isRobots ? "404 Not Found" : "200 OK";
                        var contentType = isRobots ? "text/plain" : "text/html";
                        var response = Encoding.UTF8.GetBytes(
                            $"HTTP/1.1 {status}\r\nContent-Type: {contentType}; charset=utf-8\r\n" +
                            $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
                        await tls.WriteAsync(response, serverTimeout.Token);
                        await tls.FlushAsync(serverTimeout.Token);
                    }
                }, serverTimeout.Token));
            }

            await Task.WhenAll(connections);
        }, serverTimeout.Token);
        var hostnameValidated = false;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                {
                    if (certificate is null || errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                    {
                        return false;
                    }

                    using var chain = new X509Chain();
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(root);
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    hostnameValidated = chain.Build(new X509Certificate2(certificate));
                    return hostnameValidated;
                }
            },
            ConnectCallback = (context, cancellationToken) =>
                ExternalHttpPageFetcher.ConnectPinnedAsync(
                    context,
                    cancellationToken,
                    async (pinnedEndpoint, token) =>
                    {
                        Assert.Equal(IPAddress.Parse("8.8.8.8"), pinnedEndpoint.Address);
                        Assert.Equal(443, pinnedEndpoint.Port);
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        try
                        {
                            await socket.ConnectAsync(localEndpoint, token);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    })
        };

        try
        {
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            var fetcher = new ExternalHttpPageFetcher(
                client,
                new SsrfSafeDestinationValidator(new DnsResolver(IPAddress.Parse("8.8.8.8"))),
                new RobotsPolicyEvaluator(),
                Options.Create(new ExternalFetchOptions()));

            ExternalPageResponse result;
            try
            {
                result = await fetcher.FetchAsync(new Uri("https://example.test/article"), default);
            }
            catch
            {
                await server;
                throw;
            }
            await server;

            Assert.Equal(200, result.HttpStatusCode);
            Assert.True(hostnameValidated);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void HtmlExtractor_ReturnsPlainTextMetadataAndDropsExecutableContent()
    {
        const string html = """
            <html>
              <head>
                <title>Fallback</title>
                <meta property="og:title" content="OG title">
                <meta name="description" content="Page description">
                <script type="application/ld+json">
                  {"author":{"name":"Writer"},"datePublished":"2026-08-10T01:00:00Z"}
                </script>
              </head>
              <body>
                <nav>navigation noise</nav>
                <main>
                  <h1>Main title</h1>
                  <p>First paragraph.</p>
                  <script>alert('unsafe')</script>
                  <p>Second paragraph.</p>
                </main>
              </body>
            </html>
            """;

        var result = new HtmlContentExtractor().Extract(html, new Uri("https://example.test/article"));

        Assert.Equal("OG title", result.Title);
        Assert.Equal("Page description", result.Description);
        Assert.Equal("Writer", result.AuthorName);
        Assert.Equal(new DateTime(2026, 8, 10, 1, 0, 0, DateTimeKind.Utc), result.PublishedAtUtc);
        Assert.Contains("Main title", result.Body);
        Assert.Contains("First paragraph.", result.Body);
        Assert.DoesNotContain("unsafe", result.Body);
        Assert.DoesNotContain("navigation noise", result.Body);
    }

    [Fact]
    public void HtmlExtractor_DropsIframeFallbackNestedTextAndAttributes()
    {
        const string html = """
            <main>
              <div>Visible body</div>
              <iframe title="attribute noise" src="https://remote.invalid/frame">
                fallback noise <span>nested noise</span>
              </iframe>
            </main>
            """;

        var result = new HtmlContentExtractor().Extract(html, new Uri("https://example.test/article"));

        Assert.Equal("Visible body", result.Body);
        Assert.DoesNotContain("fallback noise", result.Body);
        Assert.DoesNotContain("nested noise", result.Body);
        Assert.DoesNotContain("attribute noise", result.Body);
        Assert.DoesNotContain("remote.invalid", result.Body);
    }

    [Fact]
    public async Task Service_PreviewsWithoutMutationAndAppliesOnlyExplicitSelection()
    {
        var content = GenericContent();
        var repository = new Repository(content);
        var fetcher = new PageFetcher("""
            <html><head><title>Fetched title</title>
            <meta name="description" content="Fetched description"></head>
            <body><main><p>Fetched body</p></main></body></html>
            """);
        var service = new ExternalFetchService(
            repository,
            fetcher,
            new HtmlContentExtractor(),
            TimeProvider.System);

        var preview = await service.StartAsync(content.Id, default);

        Assert.Equal("SUCCEEDED", preview.Status);
        Assert.Equal("Before fetch", content.Title);
        Assert.Equal(IntakeStatus.URL_ACCEPTED, content.IntakeStatus);
        Assert.Null(content.DetailContent);
        Assert.Empty(repository.Evidence);

        var applied = await service.ApplyAsync(
            content.Id,
            preview.Id,
            new("Chosen title", "Chosen description", preview.Body!),
            default);

        Assert.Equal("APPLIED", applied.Attempt.Status);
        Assert.Equal("Chosen title", content.Title);
        Assert.Equal("Chosen description", content.ShortSummary);
        Assert.Equal("Fetched body", content.DetailContent);
        Assert.Equal(SourceAcquisitionMode.HTTP_METADATA, content.SourceAcquisitionMode);
        Assert.Equal(IntakeStatus.CONTENT_READY, content.IntakeStatus);
        Assert.Single(repository.Evidence);
    }

    [Fact]
    public async Task Service_PersistsFailureWithoutChangingContent()
    {
        var content = GenericContent();
        var repository = new Repository(content);
        var service = new ExternalFetchService(
            repository,
            new PageFetcher(new ExternalFetchException(
                "FETCH_TIMEOUT",
                "timeout",
                ExternalFetchFailureKind.TIMEOUT)),
            new HtmlContentExtractor(),
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<ExternalFetchException>(
            () => service.StartAsync(content.Id, default));

        Assert.Equal("FETCH_TIMEOUT", error.Code);
        var attempt = Assert.Single(repository.Attempts);
        Assert.Equal(ExternalFetchStatus.FAILED, attempt.Status);
        Assert.Equal("FETCH_TIMEOUT", attempt.ErrorCode);
        Assert.Equal("Before fetch", content.Title);
        Assert.Equal(IntakeStatus.URL_ACCEPTED, content.IntakeStatus);
    }

    private static Content GenericContent() => new()
    {
        CategoryId = CategoryCatalog.All.Single(category => category.Code == "OTHER").Id,
        Title = "Before fetch",
        CurrentWorkflowStep = WorkflowStep.URL,
        OriginalUrl = "https://example.test/article",
        NormalizedUrl = "https://example.test/article",
        SourceKind = ContentSourceKind.GENERIC,
        IntakeStatus = IntakeStatus.URL_ACCEPTED
    };

    private sealed class DnsResolver(params IPAddress[] addresses) : IDnsResolver
    {
        public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult(addresses);
    }

    private static ExternalHttpPageFetcher CreateFetcher(
        HttpMessageHandler handler,
        IDnsResolver? resolver = null,
        ExternalFetchOptions? options = null) => new(
        new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
        new SsrfSafeDestinationValidator(resolver ?? new DnsResolver(IPAddress.Parse("8.8.8.8"))),
        new RobotsPolicyEvaluator(),
        Options.Create(options ?? new ExternalFetchOptions()));

    private static HttpResponseMessage Text(HttpStatusCode status, string value, string mediaType)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(value)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return response;
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = new Uri(location, UriKind.Relative);
        return response;
    }

    private static HttpResponseMessage AbsoluteRedirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = new Uri(location, UriKind.Absolute);
        return response;
    }

    private sealed class RoutingHandler(Func<HttpRequestMessage, HttpResponseMessage> route) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(route(request));
        }
    }

    private sealed class AsyncRoutingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> route) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => route(request, cancellationToken);
    }

    private sealed class MapDnsResolver(IReadOnlyDictionary<string, IPAddress[]> addresses) : IDnsResolver
    {
        public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult(addresses[host]);
    }

    private sealed class CountingDnsResolver(IPAddress address) : IDnsResolver
    {
        public int CallCount { get; private set; }

        public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new[] { address });
        }
    }

    private sealed class TrackingContent : HttpContent
    {
        private readonly byte[] _bytes;

        public TrackingContent(string value, string mediaType)
        {
            _bytes = System.Text.Encoding.UTF8.GetBytes(value);
            Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        }

        public bool WasRead { get; private set; }
        public bool StreamDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            WasRead = true;
            return Task.FromResult<Stream>(new DisposalTrackingStream(_bytes, () => StreamDisposed = true));
        }
    }

    private sealed class DisposalTrackingStream(byte[] bytes, Action disposed) : MemoryStream(bytes)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                disposed();
            }
        }
    }

    private sealed class PageFetcher : IExternalPageFetcher
    {
        private readonly string? _html;
        private readonly Exception? _error;

        public PageFetcher(string html) => _html = html;
        public PageFetcher(Exception error) => _error = error;

        public Task<ExternalPageResponse> FetchAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (_error is not null)
            {
                return Task.FromException<ExternalPageResponse>(_error);
            }

            return Task.FromResult(new ExternalPageResponse(
                uri.AbsoluteUri,
                200,
                "text/html",
                _html!.Length,
                "SYNTHETIC_SHA256",
                null,
                null,
                _html));
        }
    }

    private sealed class Repository(Content content) : IExternalFetchRepository
    {
        public List<ExternalFetchAttempt> Attempts { get; } = [];
        public List<SourceEvidence> Evidence { get; } = [];

        public Task<Content?> FindContentAsync(Guid contentId, CancellationToken cancellationToken) =>
            Task.FromResult(contentId == content.Id ? content : null);

        public Task<ExternalFetchAttempt> CreateAttemptAsync(
            Content source,
            DateTime recentCutoffUtc,
            CancellationToken cancellationToken)
        {
            var attempt = new ExternalFetchAttempt
            {
                ContentId = source.Id,
                Content = source,
                AttemptNumber = Attempts.Count + 1
            };
            Attempts.Add(attempt);
            return Task.FromResult(attempt);
        }

        public Task<ExternalFetchAttempt?> FindAttemptAsync(
            Guid contentId,
            Guid attemptId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Attempts.SingleOrDefault(
                attempt => attempt.ContentId == contentId && attempt.Id == attemptId));

        public Task<ExternalFetchAttempt?> FindLatestAttemptAsync(
            Guid contentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Attempts.LastOrDefault(attempt => attempt.ContentId == contentId));

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ApplyAsync(
            Content source,
            ExternalFetchAttempt attempt,
            SourceEvidence evidence,
            CancellationToken cancellationToken)
        {
            Evidence.Add(evidence);
            return Task.CompletedTask;
        }
    }
}
