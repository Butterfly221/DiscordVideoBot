using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordVideoBot
{
    public class General : ModuleBase<SocketCommandContext>
    {
        [Command("echo")]
        public Task Echo([Remainder][Summary("The text to echo")] string echo)
            => ReplyAsync(echo);

    }
}
