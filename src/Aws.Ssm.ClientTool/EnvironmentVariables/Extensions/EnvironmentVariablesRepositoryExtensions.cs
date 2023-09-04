using Aws.Ssm.ClientTool.EnvironmentVariables.Rules;
using Aws.Ssm.ClientTool.Profiles;

namespace Aws.Ssm.ClientTool.EnvironmentVariables.Extensions;

public static class EnvironmentVariablesRepositoryExtensions
{
    public static IDictionary<string, string> SetFromSsmParameters(
        this IEnvironmentVariablesProvider environmentVariablesProvider,
        IDictionary<string, string> ssmParameters,
        ProfileConfig profileConfig)
    {
        var result = new SortedDictionary<string, string>();

        foreach (var ssmParam in ssmParameters)
        {
            var envVarName = EnvironmentVariableNameConverter.ConvertFromSsmPath(ssmParam.Key, profileConfig);
            
            environmentVariablesProvider.Set(envVarName, ssmParam.Value);
            
            result.Add(envVarName, ssmParam.Value);
        }
        
        return result;
    }
    
    public static IDictionary<string, string> GetAll(
        this IEnvironmentVariablesProvider environmentVariablesProvider,
        ProfileConfig profileConfig)
    {
        var result = new SortedDictionary<string, string>();
        
        var convertedEnvironmentVariableBaseNames = profileConfig.SsmPaths
            .Select(x => EnvironmentVariableNameConverter.ConvertFromSsmPath(x, profileConfig))
            .ToArray();

        var environmentVariablesToGet = environmentVariablesProvider
            .GetNames(convertedEnvironmentVariableBaseNames);

        if (environmentVariablesToGet.Any() == false)
        {
            return result;
        }

        foreach (var envVarName in environmentVariablesToGet)
        {
            var envVarValue = environmentVariablesProvider.Get(envVarName);
            
            result.Add(envVarName, envVarValue);
        }

        return result;
    }
    
    public static IDictionary<string, string> DeleteAll(
        this IEnvironmentVariablesProvider environmentVariablesProvider,
        ProfileConfig profileConfig)
    {
        var result = new SortedDictionary<string, string>();
        
        var convertedEnvironmentVariableBaseNames = profileConfig.SsmPaths
            .Select(x => EnvironmentVariableNameConverter.ConvertFromSsmPath(x, profileConfig))
            .ToArray();

        var environmentVariablesToDelete = environmentVariablesProvider
            .GetNames(convertedEnvironmentVariableBaseNames);

        if (environmentVariablesToDelete.Any() == false)
        {
            return result;
        }

        foreach (var envVarName in environmentVariablesToDelete)
        {
            var envVarValue = environmentVariablesProvider.Get(envVarName);
            
            environmentVariablesProvider.Delete(envVarName);
            
            result.Add(envVarName, envVarValue);
        }

        return result;
    }
    
    private static ISet<string> GetNames(
        this IEnvironmentVariablesProvider environmentVariablesProvider,
        IEnumerable<string> baseNames)
    {
        return baseNames
            .Select(environmentVariablesProvider.GetNames)
            .SelectMany(x => x)
            .ToHashSet();
    }
}