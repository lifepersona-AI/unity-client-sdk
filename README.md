# LifePersona Unity SDK

Unity SDK for integrating LifePersona AI conversational agents into your application. Supports both text and voice interactions.

## Requirements

- Unity 6 (6000.x) or later
- Android, iOS

## Installation

1. Download the latest `.unitypackage` from the [Releases](https://github.com/lifepersona-AI/unity-client-sdk/releases) page
2. In Unity Editor: **Assets → Import Package → Custom Package**
3. Select the downloaded file and click **Import**

## Quick Start

1. Add the `LifePersonaSDK` component to any GameObject in your scene
2. In the Inspector, set your **App Key**
3. Call `Initialize(userId)` to set up the SDK, then `StartConversation()` to connect

### Basic Usage

```csharp
using System;
using LP;
using UnityEngine;

public class MyGame : MonoBehaviour
{
    void Start()
    {
        var sdk = LifePersonaSDK.Instance;
        sdk.Initialize("player-123");

        // Subscribe to events
        sdk.OnAgentTranscript.AddListener(msg => Debug.Log($"Agent: {msg}"));
        sdk.OnUserTranscript.AddListener(msg => Debug.Log($"User: {msg}"));
        sdk.OnConversationStartedEvent.AddListener(() => Debug.Log("Connected"));
        sdk.OnConversationDisconnectedEvent.AddListener(() => Debug.Log("Disconnected"));
        sdk.OnError.AddListener(err => Debug.LogError($"SDK Error: {err}"));
    }

    // All async SDK methods return Task — use await + try/catch
    public async void OnStartClicked()
    {
        try
        {
            await LifePersonaSDK.Instance.StartConversation();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to connect: {ex.Message}");
        }
    }

    public async void OnSendClicked(string text)
    {
        try
        {
            await LifePersonaSDK.Instance.SendText(text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send: {ex.Message}");
        }
    }

    public void OnDisconnectClicked()
    {
        LifePersonaSDK.Instance.EndConversation();
    }
}
```

### Triggers

```csharp
public async void OnActivateTriggerClicked()
{
    try
    {
        var triggers = await LifePersonaSDK.Instance.GetActiveTriggers();
        if (triggers.Length > 0)
        {
            await LifePersonaSDK.Instance.ActivateTrigger(triggers[0].id);
        }
    }
    catch (Exception ex)
    {
        Debug.LogError($"Trigger failed: {ex.Message}");
    }
}
```

### Error Handling

The SDK provides two complementary error handling mechanisms:

- **`await` + `try/catch`** — for per-call error handling (recommended)
- **`OnError` UnityEvent** — global safety net that fires on every error, useful for showing error UI. Also assignable in the Inspector.

### Voice Mode

To enable voice interactions, add `PcmAudioPlayer` and `MicrophoneStreamer` components to your scene and assign them in the `LifePersonaSDK` Inspector. Set `Text Only Mode` to false.

## Demo

The `Assets/LP/Demo/` folder contains a complete chat UI example. Open `DemoScene` to see the SDK in action.
