using Discord.Interactions;

namespace NewronaBot.Commands;

/// <summary>찬양 슬래시 명령어.</summary>
public sealed class PraiseCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("찬양", "tkd님을 찬양합니다.")]
    public async Task Praise()
    {
        await RespondAsync("tkd님이 우주최강입니다 하 하 하 하 하 :smirk_cat: ");
    }
}
