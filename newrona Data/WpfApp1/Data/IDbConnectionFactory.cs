using Microsoft.Data.Sqlite;

namespace NewronaData.Data;

/// <summary>SQLite 연결 생성 추상화(DIP). 저장소는 구체 DB에 의존하지 않는다.</summary>
public interface IDbConnectionFactory
{
    SqliteConnection Create();
}
