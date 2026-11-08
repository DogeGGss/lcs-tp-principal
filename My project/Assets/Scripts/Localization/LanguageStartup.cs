using System.Collections;
using Riftwalker.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class LanguageStartup : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;
        if (LocalizationSettings.InitializationOperation.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("Localization initialization failed: " + LocalizationSettings.InitializationOperation.OperationException);
            yield break;
        }
        L10n.SetLanguage(LanguagePreference.Load(L10n.PreferencePath));
        L10n.NotifyInitialized();
        Destroy(gameObject);
    }
}
