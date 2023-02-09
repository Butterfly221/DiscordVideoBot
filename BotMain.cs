using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions;
using Microsoft;
using Discord.Addons;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Dropbox.Api;
using Dropbox.Api.Files;
using VkNet;
using VkNet.Model;
using VkNet.Enums.Filters;
using VkNet.Model.RequestParams;
using VkNet.AudioBypassService;
using Newtonsoft.Json;
using VkNet.AudioBypassService.Extensions;
using System.Text.Json;

namespace DiscordVideoBot
{
    public class BotMain
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        public BotMain()
        {
            var _config = new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 100
            };
            _client = new DiscordSocketClient(_config);
            _commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Info,
                CaseSensitiveCommands = false,
            });

            _services = ConfigureServices();
        }
        private static IServiceProvider ConfigureServices()
        {
            var map = new ServiceCollection()
                .AddSingleton(new General());
            return map.BuildServiceProvider();
        }
        public async Task MainAsync()
        {
            try {
                await InitCommands();
                HttpClient client = new HttpClient();

                var token = "";
                await _client.LoginAsync(TokenType.Bot, token);
                await _client.StartAsync();

            } 
            catch (AggregateException ex)
            {
                foreach (var e in ex.InnerExceptions)
                {
                    Console.WriteLine("Error: " + e.Message);
                    Console.ReadLine();
                }
            }
            _client.MessageUpdated += MessageUpdated;
            _client.Ready += () =>
            {
                //Console.WriteLine("Bot is connected!");
                return Task.CompletedTask;
            };
            string dir = Directory.GetCurrentDirectory()+@"/Upload";
            Upload up = new Upload();
            while (Directory.GetFiles(dir) != null)
            {
                try
                {
                    up.Run().Wait();
                    var chnl = _client.GetGuild(302843530420813826).GetChannel(394587466511941642) as IMessageChannel;
                    string video = "https://vk.com/video"+up.user_id+"_"+up.id;
                    if (chnl != null) await chnl.SendMessageAsync(video);
                    Console.ReadLine();
                }
                catch (AggregateException ex)
                {
                    foreach (var e in ex.InnerExceptions)
                    {
                        Console.WriteLine("Error: " + e.Message);
                        Console.ReadLine();
                    }
                }
            }
            await Task.Delay(-1);
        }
        private async Task MessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            var message = await before.GetOrDownloadAsync();
            Console.WriteLine($"{message} -> {after}");
        }
        private async Task InitCommands()
        {
            //await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            await _commands.AddModuleAsync<General>(_services);
            _client.MessageReceived += OnMessageReceived;

        }
        private async Task OnCommandExecuted(Optional<CommandInfo> commandInfo, ICommandContext commandContext, IResult result)
        {
            if (result.IsSuccess)
            {
                return;
            }
            await commandContext.Channel.SendMessageAsync(result.ErrorReason);
        }
        private async Task OnMessageReceived(SocketMessage socketMsg)
        {
            var message = socketMsg as SocketUserMessage;
            if (message == null) return;
            var argPos = 0;
            if (!(message.HasCharPrefix('!', ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            var context = new SocketCommandContext(_client, message);
            await _commands.ExecuteAsync(context, argPos, services: null);
        }
    }
    public class LoggingService
    {
        public LoggingService(DiscordSocketClient client, CommandService command)
        {
            client.Log += LogAsync;
            command.Log += LogAsync;
        }
        private Task LogAsync(LogMessage message)
        {
            if (message.Exception is CommandException cmdException)
           {
                Console.WriteLine($"[Command/{message.Severity}] {cmdException.Command.Aliases.First()}" + $"failed to execute in {cmdException.Context.Channel}.");
                Console.WriteLine(cmdException);
            }
            else Console.WriteLine($"[General/{message.Severity}]{message}");
        return Task.CompletedTask;
        }
    }
    public class Upload
    {
        string login, pass;
        public string id = "0";
        public string user_id;
        VkApi api;
        public async Task Run()
        {
            while (api == null)
            {
                login = "";
                pass = "";
                var services = new ServiceCollection();
                services.AddAudioBypass();
                api = new VkApi(services);
                Uri uri = new Uri("https://oauth.vk.com/blank.html");
                try
                {
                    api.Authorize(new ApiAuthParams
                    {
                        Login = login,
                        Password = pass,
                        ApplicationId = 51410200,
                        RedirectUri = uri,
                        Settings = Settings.Video,
                        ResponseType = VkNet.Enums.SafetyEnums.ResponseType.Token,
                        TwoFactorAuthorization = () =>
                        {
                            Console.WriteLine("Enter Code:");
                            return Console.ReadLine();
                        }
                });
                    Console.WriteLine("Вы успешно авторизовались!");
                    user_id = api.UserId.ToString();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Данные неверны.");
                    api = null;
                }
            }
            Console.WriteLine("Начало выгрузки, ожидайте...");
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(30);
            VkNet.Model.Attachments.Video video = api.Video.Save(new VideoSaveParams
            {
                Name = "Fight",
                Wallpost = false,
            });
            DateTime dt = new DateTime(1990, 1, 1);
            string fileName = "";
            FileSystemInfo[] fileSystemInfo = new DirectoryInfo(Directory.GetCurrentDirectory() + "\\Upload\\Albion Online").GetFileSystemInfos();
            foreach (FileSystemInfo fileSI in fileSystemInfo)
            {
                if (fileSI.Extension == ".mp4")
                {
                    if (dt < Convert.ToDateTime(fileSI.CreationTime))
                    {
                        dt = Convert.ToDateTime(fileSI.CreationTime);
                        fileName = fileSI.Name;
                    }
                }
            }

            string filePath = Directory.GetCurrentDirectory() + @"/Upload/Albion Online/" + fileName;
            int length = (int)new System.IO.FileInfo(filePath).Length;
            byte [] file = File.ReadAllBytes(filePath);
            var Content = new ByteArrayContent(file);
            var multipartContent = new MultipartFormDataContent();
            multipartContent.Add(Content, "video_file", fileName);
            var responce = await client.PostAsync(video.UploadUrl, multipartContent);
            string httpContent = await responce.Content.ReadAsStringAsync();
            string Jsonstring = httpContent;
            Videofile resp = System.Text.Json.JsonSerializer.Deserialize<Videofile>(Jsonstring);
            Console.WriteLine("Видео успешно выгружено!");
            id = resp.video_id.ToString();
            File.Move(Directory.GetCurrentDirectory() + @"\Upload\Albion Online\" + fileName, Directory.GetCurrentDirectory() + @"\Already\" + fileName);
        }
    }



}
