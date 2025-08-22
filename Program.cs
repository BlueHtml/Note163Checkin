using PuppeteerSharp;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;

const int TIMEOUT_MS = 60_000;

Conf _conf = Deserialize<Conf>(GetEnvValue("CONF"));
HttpClient _scClient = new();

#region redis

ConnectionMultiplexer redis = ConnectionMultiplexer.Connect($"{_conf.RdsServer},password={_conf.RdsPwd},name=Note163Checkin,defaultDatabase=0,allowadmin=true,abortConnect=false");
IDatabase db = redis.GetDatabase();
bool isRedis = db.IsConnected("test");
Console.WriteLine("redis:{0}", isRedis ? "有效" : "无效");

#endregion

Console.WriteLine("有道云笔记签到开始运行...");
for (int i = 0; i < _conf.Users.Length; i++)
{
    User user = _conf.Users[i];
    string title = $"账号 {i + 1}: {user.Task} ";
    Console.WriteLine($"共 {_conf.Users.Length} 个账号，正在运行{title}...");

    #region 获取cookie

    string cookie = string.Empty;
    bool isInvalid = true; string result = string.Empty;

    if (!string.IsNullOrWhiteSpace(user.Cookie))
    {
        Console.WriteLine("json-cookie存在,开始验证...");
        cookie = user.Cookie;
        (isInvalid, result) = await IsInvalid(cookie);
        Console.WriteLine("json-cookie状态:{0}", isInvalid ? "无效" : "有效");
    }

    if (isInvalid)
    {
        string redisKey = $"Note163_{user.Username}";
        if (isRedis)
        {
            var redisValue = await db.StringGetAsync(redisKey);
            if (redisValue.HasValue)
            {
                cookie = redisValue.ToString();
                (isInvalid, result) = await IsInvalid(cookie);
                Console.WriteLine("redis获取cookie,状态:{0}", isInvalid ? "无效" : "有效");
            }
        }

        if (isInvalid)
        {
            cookie = await GetCookie(user);
            (isInvalid, result) = await IsInvalid(cookie);
            Console.WriteLine("login获取cookie,状态:{0}", isInvalid ? "无效" : "有效");
            if (isInvalid)
            {//Cookie失效
                await Notify($"{title}Cookie失效，请检查登录状态！", true);
                continue;
            }
        }

        if (isRedis)
        {
            Console.WriteLine($"redis更新cookie:{await db.StringSetAsync(redisKey, cookie)}");
        }
    }

    #endregion

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "ynote-android");
    client.DefaultRequestHeaders.Add("Cookie", cookie);

    long space = 0;
    space += Deserialize<YdNoteRsp>(result).RewardSpace;

    //签到
    result = await (await client.PostAsync("https://note.youdao.com/yws/mapi/user?method=checkin", null))
       .Content.ReadAsStringAsync();
    space += Deserialize<YdNoteRsp>(result).Space;

    //看广告
    for (int j = 0; j < 3; j++)
    {
        result = await (await client.PostAsync("https://note.youdao.com/yws/mapi/user?method=adPrompt", null))
           .Content.ReadAsStringAsync();
        space += Deserialize<YdNoteRsp>(result).Space;
    }

    //看视频广告
    for (int j = 0; j < 3; j++)
    {
        result = await (await client.PostAsync("https://note.youdao.com/yws/mapi/user?method=adRandomPrompt", null))
           .Content.ReadAsStringAsync();
        space += Deserialize<YdNoteRsp>(result).Space;
    }

    await Notify($"有道云笔记{title}签到成功，共获得空间 {space / 1048576} M");
}
Console.WriteLine("签到运行完毕");

async Task<(bool isInvalid, string result)> IsInvalid(string cookie)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "ynote-android");
    client.DefaultRequestHeaders.Add("Cookie", cookie);
    //每日打开客户端（即登陆）
    string result = await (await client.PostAsync("https://note.youdao.com/yws/api/daupromotion?method=sync", null))
        .Content.ReadAsStringAsync();
    return (result.Contains("error", StringComparison.OrdinalIgnoreCase), result);
}

async Task<string> GetCookie(User user)
{
    var launchOptions = new LaunchOptions
    {
        Headless = false,
        DefaultViewport = null,
        ExecutablePath = @"/usr/bin/google-chrome"
    };
    var browser = await Puppeteer.LaunchAsync(launchOptions);
    IPage page = await browser.DefaultContext.NewPageAsync();

    await page.GoToAsync(_conf.LoginUrl, TIMEOUT_MS);

    bool isLogin = false;
    string cookie = "fail";
    try
    {
        #region 登录

        //登录
        _ = Login(page, user);
        int totalDelayMs = 0, delayMs = 100;
        while (true)
        {
            if ((isLogin = IsLogin(page))
                || totalDelayMs > TIMEOUT_MS)
            {
                break;
            }
            await Task.Delay(delayMs);
            totalDelayMs += delayMs;
        }

        if (isLogin)
        {
            var client = await page.CreateCDPSessionAsync();
            var ckObj = await client.SendAsync("Network.getAllCookies");
            var cks = ckObj.Value.GetProperty("cookies").EnumerateArray()
                .Where(p => p.GetProperty("domain").GetString().Contains("note.youdao.com"))
                .Select(p => $"{p.GetProperty("name").GetString()}={p.GetProperty("value").GetString()}");
            cookie = string.Join(';', cks);
        }

        #endregion
    }
    catch (Exception ex)
    {
        cookie = "ex";
        Console.WriteLine($"处理Page时出现异常！{ex.Message}；{ex.StackTrace}");
    }
    finally
    {
        await browser.DisposeAsync();
    }

    return cookie;
}

async Task Login(IPage page, User user)
{
    try
    {
        string js = await _scClient.GetStringAsync(_conf.JsUrl);
        await page.EvaluateExpressionAsync(js.Replace("@U", user.Username).Replace("@P", user.Password));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Login时出现异常！{ex.Message}. {ex.StackTrace}");
    }
}

bool IsLogin(IPage page) => !page.Url.Contains(_conf.LoginStr, StringComparison.OrdinalIgnoreCase);

async Task Notify(string msg, bool isFailed = false)
{
    Console.WriteLine(msg);
    if (_conf.ScType == "Always" || (isFailed && _conf.ScType == "Failed"))
    {
        string scKey = _conf.ScKey.Trim();
        int index = scKey.IndexOf(' ');
        if (index == -1)
        {
            await _scClient.GetAsync($"https://sc.ftqq.com/{scKey}.send?text={msg}");
        }
        else
        {
            // 要执行的命令
            string command = scKey[..index].Trim();
            // 要传递给命令的参数
            string arguments = scKey[index..].Trim().Replace("$title", "Note163Checkin").Replace("$msg", msg);
            Console.WriteLine($"准备执行命令: {command}");

            #region 执行命令

            // 创建 ProcessStartInfo 对象
            var processStartInfo = new ProcessStartInfo
            {
                FileName = command, // 在 Linux 上，系统会从 PATH 环境变量中查找 'curl'
                Arguments = arguments,
                RedirectStandardOutput = true, // 重定向标准输出
                RedirectStandardError = true,  // 重定向标准错误
                UseShellExecute = false,       // 必须为 false 才能重定向流
                CreateNoWindow = true,         // 在非 GUI 环境下通常设置为 true
            };

            // 创建并启动进程
            using (var process = new Process { StartInfo = processStartInfo })
            {
                try
                {
                    process.Start();

                    // 异步读取输出和错误流
                    // 这比同步读取更好，可以避免死锁
                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();

                    // 等待进程执行完成
                    await process.WaitForExitAsync();

                    // 获取输出结果
                    string output = await outputTask;
                    string error = await errorTask;

                    Console.WriteLine("-------------------- 命令执行完成 --------------------");

                    // 检查退出码
                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine("命令成功执行。");
                        Console.WriteLine("\n--- 标准输出 (stdout) ---");
                        // 只打印部分输出，因为 HTML 内容可能很长
                        Console.WriteLine(output.Length > 500 ? output.Substring(0, 500) + "..." : output);
                    }
                    else
                    {
                        Console.WriteLine($"命令执行失败，退出码: {process.ExitCode}");
                        if (!string.IsNullOrEmpty(error))
                        {
                            Console.WriteLine("\n--- 标准错误 (stderr) ---");
                            Console.WriteLine(error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"执行命令时发生异常: {ex.Message}");
                    // 在这里可以处理一些情况，例如 'curl' 命令不存在
                }
            }

            #endregion
        }
    }
}

T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
});

string GetEnvValue(string key) => Environment.GetEnvironmentVariable(key);

#region Conf

class Conf
{
    public User[] Users { get; set; }
    public string ScKey { get; set; }
    public string ScType { get; set; }
    public string RdsServer { get; set; }
    public string RdsPwd { get; set; }
    public string LoginUrl { get; set; } = "https://note.youdao.com/mobileSignIn/login_mobile.html?&back_url=https://note.youdao.com/web/&from=web";
    public string LoginStr { get; set; } = "signIn";
    public string JsUrl { get; set; } = "https://github.com/BlueHtml/pub/raw/main/code/js/note163login.js";
}

class User
{
    public string Task { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Cookie { get; set; }
}

#endregion

class YdNoteRsp
{
    /// <summary>
    /// Sync奖励空间
    /// </summary>
    public int RewardSpace { get; set; }

    /// <summary>
    /// 其他奖励空间
    /// </summary>
    public int Space { get; set; }
}
