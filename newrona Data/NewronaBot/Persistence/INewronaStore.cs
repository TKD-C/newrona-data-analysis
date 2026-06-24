namespace NewronaBot.Persistence;

/// <summary>
/// 봇 데이터 저장소 추상화.
/// 메모리에 올라온 <see cref="NewronaDatabase"/>가 실행 중 실시간 원본(single source of truth)이고,
/// 구현체는 그 상태를 외부(디스코드 채널/파이어스토어 등)에 영속화한다.
/// 저장소를 갈아끼워도 저장소(repository)·서비스 계층은 그대로 재사용된다.
/// </summary>
public interface INewronaStore
{
    /// <summary>외부 저장소에서 데이터를 읽어 메모리에 적재(시작 시 1회).</summary>
    Task InitializeAsync();

    /// <summary>읽기 전용 접근(잠금 하에 스냅샷 함수 실행).</summary>
    T Read<T>(Func<NewronaDatabase, T> read);

    /// <summary>변경 작업(잠금 하에 실행 후 저장 예약).</summary>
    void Mutate(Action<NewronaDatabase> mutate);

    /// <summary>현재 메모리 상태를 즉시 외부 저장소에 저장(종료 시에도 호출).</summary>
    Task FlushAsync();
}
