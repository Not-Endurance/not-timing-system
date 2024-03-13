using Core.Services;
using Core.Utilities;
using EMS.Judge.Api.Configuration;

namespace EMS.Judge.Api;

public class JudgeApiInitializer : IInitializer  
{
    private IJudgeServiceProvider _judgeServiceProvider;
    public JudgeApiInitializer(IJudgeServiceProvider judgeServiceProvider)
    {
        _judgeServiceProvider = judgeServiceProvider;
    }

    public int RunningOrder => 0;
    public void Run() {
        StaticProvider.Initialize(_judgeServiceProvider);
    }
}