using System.ComponentModel.DataAnnotations;
using Aws.Ssm.Cli.EnvironmentVariables;
using Aws.Ssm.Cli.EnvironmentVariables.Rules;
using Aws.Ssm.Cli.Helpers;
using Aws.Ssm.Cli.Profiles;
using Aws.Ssm.Cli.Profiles.Rules;
using Aws.Ssm.Cli.SsmParameters;
using Aws.Ssm.Cli.SsmParameters.Rules;
using Aws.Ssm.Cli.EnvironmentVariables.Extensions;
using Aws.Ssm.Cli.Profiles.Extensions;
using Aws.Ssm.Cli.SsmParameters.Extensions;
using Sharprompt;

namespace Aws.Ssm.Cli.Commands.Handlers;

public class ConfigProfileCommandHandler : ICommandHandler
{
    private readonly IProfileConfigProvider _profileConfigProvider;

    private readonly IEnvironmentVariablesProvider _environmentVariablesProvider;
    
    private readonly ISsmParametersProvider _ssmParametersProvider;
    
    private enum OperationEnum
    {
        New,
        Delete,
        Edit,
    }

    public ConfigProfileCommandHandler(
        IProfileConfigProvider profileConfigProvider,
        IEnvironmentVariablesProvider environmentVariablesProvider,
        ISsmParametersProvider ssmParametersProvider)
    {
        _profileConfigProvider = profileConfigProvider;

        _environmentVariablesProvider = environmentVariablesProvider;

        _ssmParametersProvider = ssmParametersProvider;
    }

    public string CommandName => "config";

    public string Description => "Profile(s) configuration";

    public Task Handle(CancellationToken cancellationToken)
    {
        ConsoleHelper.WriteLineNotification($"START - {Description}");
        Console.WriteLine();

        var profileDetails = GetProfileDetailsForConfiguration();

        var backupProfileDo = profileDetails.ProfileDo.Clone();
        
        if (profileDetails.Operation == OperationEnum.New)
        {
            SpinnerHelper.Run(
                () => _profileConfigProvider.Save(profileDetails.ProfileName, profileDetails.ProfileDo),
                $"Save new profile [{profileDetails.ProfileName}] configuration with default settings");
        }

        profileDetails.ProfileDo.PrintProfileSettings();

        if (profileDetails.Operation == OperationEnum.Delete)
        {
            SpinnerHelper.Run(
                () => _profileConfigProvider.Delete(profileDetails.ProfileName),
                $"Delete profile [{profileDetails.ProfileName}]");
            
            ConsoleHelper.WriteLineInfo($"DONE - Deleted profile [{profileDetails.ProfileName}]");

            return Task.CompletedTask;
        }

        var allowToExit = false;

        while (!allowToExit)
        {
            var completeOperationName = "Complete/exit configuration"; 
            var removeSsmPathOperationName = "Remove ssm-path(s)";
            var manageOperationsLookup = new Dictionary<string, Func<ProfileConfig, bool>>
            {
                { completeOperationName, Exit },
                { "Set prefix", SetEnvironmentVariablePrefix },
                { "Add ssm-path (available only)", (profile) => AddSsmPath(profile, allowAddUnavailableSsmPath: false) },
                { "Add ssm-path (ignore availability)", (profile) => AddSsmPath(profile, allowAddUnavailableSsmPath: true) },
                { removeSsmPathOperationName, RemoveSsmPaths },
           };
            if (profileDetails.ProfileDo.SsmPaths.Any() != true)
            {
                manageOperationsLookup.Remove(removeSsmPathOperationName);
            }

            var operationKey = Prompt.Select(
                "Select operation",
                items: manageOperationsLookup.Keys,
                defaultValue: manageOperationsLookup.Keys.First());

            var operationFunction = manageOperationsLookup[operationKey];

            var hasChanges = operationFunction(profileDetails.ProfileDo);

            if (hasChanges)
            {
                if (profileDetails.Operation != OperationEnum.New &&
                    profileDetails.ProfileName == _profileConfigProvider.ActiveName &&
                    backupProfileDo?.IsValid == true)
                {
                    ConsoleHelper.WriteLineNotification($"Deactivate profile [{profileDetails.ProfileName}] before any configuration changes");

                    SpinnerHelper.Run(
                        () => _environmentVariablesProvider.DeleteAll(profileDetails.ProfileDo),
                        "Delete active environment variables");

                    _profileConfigProvider.ActiveName = null;

                    backupProfileDo = null; // Reset deactivated backup profile
                }

                SpinnerHelper.Run(
                    () => _profileConfigProvider.Save(profileDetails.ProfileName, profileDetails.ProfileDo),
                    $"Save profile [{profileDetails.ProfileName}] configuration new settings");
            
                profileDetails.ProfileDo.PrintProfileSettings();
            }
            
            allowToExit = operationKey == completeOperationName;
        }

        ConsoleHelper.WriteLineInfo($"DONE - Profile [{profileDetails.ProfileName}] configuration");
        Console.WriteLine();

        ConsoleHelper.WriteLineNotification($"START - View profile [{profileDetails.ProfileName}] configuration");
        Console.WriteLine();

        if (profileDetails.ProfileDo.IsValid != true)
        {
            ConsoleHelper.WriteLineError($"Not configured profile [{profileDetails.ProfileName}]");

            return Task.CompletedTask;
        }

        var resolvedSsmParameters = SpinnerHelper.Run(
            () => _ssmParametersProvider.GetDictionaryBy(profileDetails.ProfileDo.SsmPaths),
            "Get ssm parameters from AWS System Manager");
        
        resolvedSsmParameters.PrintSsmParameters(profileDetails.ProfileDo);

        resolvedSsmParameters.PrintSsmParameterToEnvironmentVariableNamesMapping(
            profileDetails.ProfileDo);

        resolvedSsmParameters.PrintSsmParametersToEnvironmentVariables(
            profileDetails.ProfileDo);

        ConsoleHelper.WriteLineInfo($"DONE - View profile [{profileDetails.ProfileName}] configuration");

        return Task.CompletedTask;
    }

    private (OperationEnum Operation, string ProfileName, ProfileConfig ProfileDo) GetProfileDetailsForConfiguration()
    {
        var profileNames = SpinnerHelper.Run(
            _profileConfigProvider.GetNames,
            "Get profile names");

        var lastActiveProfileName = _profileConfigProvider.ActiveName;
        if (!string.IsNullOrEmpty(lastActiveProfileName))
        {
            ConsoleHelper.WriteLineNotification($"Current active profile is [{lastActiveProfileName}]");
        }

        var operation = OperationEnum.New;
        var profileName = "default";
        var profileDo = new ProfileConfig();

        if (profileNames.Any())
        {
            operation = Prompt.Select(
                "Select profile operation",
                items: new[] { OperationEnum.Edit, OperationEnum.New, OperationEnum.Delete },
                defaultValue: OperationEnum.Edit);
        }

        if (operation == OperationEnum.New)
        {
            profileName = Prompt.Input<string>(
                "Enter new profile name ",
                defaultValue: profileName,
                validators: new List<Func<object, ValidationResult>>
                {
                    (check) => ProfileNameValidationRule.Handle((string) check, profileNames),
                }).Trim();
            
            return (operation, profileName, profileDo);
        }

        profileName =
            profileNames.Count == 1
                ? profileNames.Single()
                : Prompt.Select(
                    "Select profile",
                    items: profileNames,
                    defaultValue: lastActiveProfileName);

        profileDo = 
            SpinnerHelper.Run(
                () => _profileConfigProvider.GetByName(profileName),
                $"Read profile [{profileName}]")
            ?? new ProfileConfig(); 

        return (operation, profileName, profileDo);
    }

    private bool Exit(ProfileConfig profileConfig) => false;
    
    private bool SetEnvironmentVariablePrefix(ProfileConfig profileConfig)
    {
        profileConfig.EnvironmentVariablePrefix = Prompt.Input<string>(
            $"Set {nameof(profileConfig.EnvironmentVariablePrefix)} (space is undefined)",
            defaultValue: profileConfig.EnvironmentVariablePrefix ?? " ",
            validators: new List<Func<object, ValidationResult>>
            {
                (check) => EnvironmentVariableNameValidationRule.HandlePrefix((string) check),
            }).Trim();

        return true;
    }
    
    private bool AddSsmPath(ProfileConfig profileConfig, bool allowAddUnavailableSsmPath)
    {
        var newSsmPath = Prompt.Input<string>(
            "Enter new ssm-path (start from the /)",
            validators: new List<Func<object, ValidationResult>>
            {
                (check) =>
                {
                    if (check == null)
                    {
                        return ValidationResult.Success;
                    }
                    
                    return SsmPathValidationRules.Handle(
                        (string) check,
                        profileConfig.SsmPaths);
                },
                (check) =>
                {
                    if (check == null)
                    {
                        return ValidationResult.Success;
                    }
                    
                    return CheckSsmPathAvailability(check.ToString(), allowAddUnavailableSsmPath);
                },
            })?.Trim();

        if (!string.IsNullOrWhiteSpace(newSsmPath))
        {
            profileConfig.SsmPaths = new HashSet<string>(
                profileConfig.SsmPaths
                    .Union(new [] { newSsmPath })
                    .OrderBy(x => x));

            return true;
        }

        return false;
    }

    private ValidationResult CheckSsmPathAvailability(string check, bool allowAddUnavailableSsmPath)
    {
        var ssmParameters = SpinnerHelper.Run(
            () => _ssmParametersProvider.GetDictionaryBy(new HashSet<string> { check, }),
            "Get ssm parameters from AWS System Manager to validate the ssm-path");

        if (ssmParameters?.Any() != true && !allowAddUnavailableSsmPath)
        {
            return new ValidationResult("Unavailable ssm path");
        }

        return ValidationResult.Success;
    }
    
    private bool RemoveSsmPaths(ProfileConfig profileConfig)
    {
        var ssmPathsToDelete = Prompt
            .MultiSelect(
                "- Select ssm-path(s) to delete",
                profileConfig.SsmPaths,
                minimum: 0)
            .OrderBy(x => x)
            .ToArray();
        
        profileConfig.SsmPaths.ExceptWith(ssmPathsToDelete);

        return ssmPathsToDelete.Length > 0;
    }
 }