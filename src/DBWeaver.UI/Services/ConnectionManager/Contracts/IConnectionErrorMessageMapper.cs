using DBWeaver.Core;

namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionErrorMessageMapper
{
    string Map(Exception ex, DatabaseProvider provider);
}

