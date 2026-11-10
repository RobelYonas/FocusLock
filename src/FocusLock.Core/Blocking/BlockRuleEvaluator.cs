using FocusLock.Core.Abstractions;
using FocusLock.Core.Common;
using FocusLock.Core.Detection;

namespace FocusLock.Core.Blocking;

/// <summary>Decides whether a process matches a policy. Never blocks FocusLock itself or anything in the Windows folder.</summary>
public sealed class BlockRuleEvaluator(IAppEnvironment environment)
{
    public BlockDecision Evaluate(BlockPolicy policy, ProcessInfo process)
    {
        if (process.Id == environment.ProcessId)
        {
            return BlockDecision.Allow;
        }

        if (process.ExecutablePath is not null && PathNormalizer.IsUnder(process.ExecutablePath, environment.WindowsDirectory))
        {
            return BlockDecision.Allow;
        }

        if (policy.Allow.Any(rule => Matches(rule, process)))
        {
            return BlockDecision.Allow;
        }

        var matched = policy.Rules.FirstOrDefault(rule => Matches(rule, process));
        return matched is null ? BlockDecision.Allow : BlockDecision.Block(matched);
    }

    private static bool Matches(BlockRule rule, ProcessInfo process)
    {
        var path = process.ExecutablePath;
        switch (rule.Kind)
        {
            case MatchKind.ProcessName:
                return PathNormalizer.NameEquals(process.Name, rule.Target);

            case MatchKind.Executable:
                return path is not null
                    ? PathNormalizer.PathEquals(path, rule.Target)
                    : PathNormalizer.NameEquals(process.Name, Path.GetFileName(rule.Target));

            case MatchKind.Folder:
                return path is not null
                    ? PathNormalizer.IsUnder(path, rule.Target)
                    : rule.ExecutableNames?.Any(name => PathNormalizer.NameEquals(name, process.Name)) == true;

            default:
                return false;
        }
    }
}
