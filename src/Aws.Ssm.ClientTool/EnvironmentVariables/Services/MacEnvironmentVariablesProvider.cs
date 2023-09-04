using Aws.Ssm.ClientTool.EnvironmentVariables.Rules;
using Aws.Ssm.ClientTool.Helpers;
using Aws.Ssm.ClientTool.UserRuntime;
using Microsoft.Extensions.Logging;

namespace Aws.Ssm.ClientTool.EnvironmentVariables.Services;

public class MacEnvironmentVariablesProvider : DefaultEnvironmentVariablesProvider
{
    private readonly IUserFilesProvider _userFilesProvider;

    private readonly ILogger<MacEnvironmentVariablesProvider> _logger;

    private Dictionary<string, string> _loadedDescriptor = null;

    public MacEnvironmentVariablesProvider(
        IUserFilesProvider userFilesProvider,
        ILogger<MacEnvironmentVariablesProvider> logger)
    {
        _userFilesProvider = userFilesProvider;

        _logger = logger;
    }

    public override void Set(string name, string value)
    {
        var environmentVariables = LoadEnvironmentVariablesFromDescriptor();

        environmentVariables[name] = value;
        
        DumpEnvironmentVariables(environmentVariables);
        
        Environment.SetEnvironmentVariable(name, value);
    }

    public override void Delete(string name)
    {
        var environmentVariables = LoadEnvironmentVariablesFromDescriptor();

        environmentVariables.Remove(name);
        
        DumpEnvironmentVariables(environmentVariables);

        Environment.SetEnvironmentVariable(name, null);
    }

    private Dictionary<string, string> LoadEnvironmentVariablesFromDescriptor()
    {
        if (_loadedDescriptor != null)
        {
            return _loadedDescriptor;
        }
        
        var fileDescriptorName = EnvironmentVariablesConsts.FileNames.Descriptor;

        try
        {
            var fileDescriptorText = _userFilesProvider.ReadTextFileIfExist(fileDescriptorName);

            if (!string.IsNullOrEmpty(fileDescriptorText))
            {
                _loadedDescriptor = JsonSerializationHelper.Deserialize<Dictionary<string, string>>(fileDescriptorText);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Error on attempt to read descriptor file with list of environment variables");
        }

        return _loadedDescriptor ?? new Dictionary<string, string>();
    }

    private void DumpEnvironmentVariables(Dictionary<string, string> environmentVariables)
    {
        var fileDescriptorName = EnvironmentVariablesConsts.FileNames.Descriptor;
        var fileScriptName = EnvironmentVariablesConsts.FileNames.Script;

        try
        {
            var fileDescriptorText = JsonSerializationHelper.Serialize(environmentVariables);
            _userFilesProvider.WriteTextFile(fileDescriptorName, fileDescriptorText);

            var fileScriptText = EnvironmentVariablesScriptBuilder.Build(environmentVariables);
            _userFilesProvider.WriteTextFile(fileScriptName, fileScriptText);
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Error on attempt to dump descriptor/script file(s) with list of environment variables");
        }
    }
}