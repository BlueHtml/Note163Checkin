using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Note163Checkin
{
    class Program
    {
        static Conf _conf;
        static HttpClient _scClient;

        static async Task Main()
        {
            _conf = Deserialize<Conf>(GetEnvValue("CONF"));
            if (!string.IsNullOrWhiteSpace(_conf.ScKey))
            {
                _scClient = new HttpClient();
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "ynote-android");
            Console.WriteLine("有道云笔记签到开始运行...");

            for (int i = 0; i < _conf.Users.Length; i++)
            {
                User user = _conf.Users[i];
                string title = $" 账号 {i + 1}: {user.Name} ";
                Console.WriteLine($"共 {_conf.Users.Length} 个账号，正在运行{title}...");

                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", user.Cookie);

                //每日打开客户端（即登陆）
                string result = await (await client.PostAsync("https://note.youdao.com/yws/api/daupromotion?method=sync", null))
                    .Content.ReadAsStringAsync();
                if (result.Contains("error", StringComparison.OrdinalIgnoreCase))
                {//Cookie失效
                    await Notify($"{title}Cookie失效，请及时更新！", true);
                    continue;
                }

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
        }

        static async Task Notify(string msg, bool isFailed = false)
        {
            Console.WriteLine(msg);
            if (_conf.ScType == "Always" || (isFailed && _conf.ScType == "Failed"))
            {
                await _scClient?.GetAsync($"https://sc.ftqq.com/{_conf.ScKey}.send?text={msg}");
            }
        }

        static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);

        static string GetEnvValue(string key) => Environment.GetEnvironmentVariable(key);
    }

    #region Conf

    public class Conf
    {
        public User[] Users { get; set; }
        public string ScKey { get; set; }
        public string ScType { get; set; }
    }

    public class User
    {
        public string Name { get; set; }
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
}
