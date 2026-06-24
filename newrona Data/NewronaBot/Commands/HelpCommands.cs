using Discord;
using Discord.Interactions;

namespace NewronaBot.Commands;

/// <summary>도움말 슬래시 명령어(응답은 본인만 보이는 ephemeral).</summary>
public sealed class HelpCommands : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>일반 유저용 도움말 — 누구나 쓸 수 있는 명령만 쉽고 간결하게 안내.</summary>
    [SlashCommand("도움말", "쓸 수 있는 명령어와 사용법을 봅니다.")]
    public async Task Help()
    {
        var embed = new EmbedBuilder()
            .WithTitle("📖 도움말")
            .WithColor(Color.Green)
            .WithDescription("아래 명령어를 채팅창에 `/` 와 함께 입력하면 됩니다.\n*(이 도움말은 나만 볼 수 있어요.)*")
            .AddField("👥 내전러 보기",
                "`/내전러목록` — 등록된 사람들의 점수·등급·전적을 봅니다.\n" +
                "`/내전러정보` — 내전러 한 명의 정보(점수·등급·라인·반창고)를 봅니다.")
            .AddField("🏆 등급 보기",
                "`/서버내등급` — 우리 서버 사람들의 등급 순위를 봅니다.\n" +
                "`/등급기준표` — 몇 점이면 어떤 등급인지 봅니다.")
            .AddField("🐱 기타", "`/찬양` — tkd님을 찬양합니다.")
            .WithFooter("관리자라면 /관리자도움말 로 관리 명령을 볼 수 있어요.")
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    /// <summary>관리자용 도움말 — 내전관리자 역할만 실행 가능.</summary>
    [RequireRole("내전관리자")]
    [SlashCommand("관리자도움말", "[내전관리자] 관리자 명령어를 포함해 모두 봅니다.")]
    public async Task AdminHelp()
    {
        var embed = new EmbedBuilder()
            .WithTitle("🛠️ 관리자 도움말")
            .WithColor(Color.Red)
            .WithDescription("내전관리자 역할 전용 명령어 모음입니다.\n*(이 도움말은 나만 볼 수 있어요.)*")
            .AddField("👥 내전러 관리",
                "`/내전러추가` — 새 내전러 등록\n" +
                "`/내전러수정` — 내전러 정보 수정\n" +
                "`/내전러삭제` — 내전러 삭제")
            .AddField("🔑 PUUID 관리(비밀)",
                "`/내전러puuid설정` — PUUID 직접 입력\n" +
                "`/내전러puuid연결` — 롤 ID(`AAA#BB`)로 PUUID 자동 연결\n" +
                "`/내전러puuid조회` — PUUID 확인")
            .AddField("📜 경기 관리",
                "`/경기기록` — 5대5 경기 수동 기록\n" +
                "`/경기목록` — 최근 경기 기록 보기\n" +
                "`/경기삭제` — 경기 1건 삭제(번호)\n" +
                "`/내전기록삭제` — 여러 경기 한 번에 삭제\n" +
                "`/내전기록하기` * — 참여자 1명 지정 → 그 사람 최근 20경기 중 커스텀만 자동 기록")
            .AddField("🧩 팀 편성",
                "`/팀짜주기뱀` — 점수순 스네이크 드래프트\n" +
                "`/팀짜주기랜덤` — 팀 평균 점수 비슷하게 랜덤\n" +
                "`/팀짜주기라인` * — 주라인 위주(부라인·반창고·균형 고려)")
            .AddField("ℹ️ 안내", "`*` 표시는 주로 사용되는 명령이에요.")
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}
