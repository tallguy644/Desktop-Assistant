# Set up ASSISTANT's natural voice

The app supports **Yan / English (Hong Kong)** for a lighter Chinese-influenced English option, **Nanami / English / Japanese accent**, and **Xiaoxiao / English / Mandarin Chinese accent**, alongside Luna, Jenny, and British voices. Accent strength is fixed by the synthetic voice; compare them with PREVIEW. Your existing **-15% speed**, volume, and Azure connection are retained. These are original synthetic voices. New installations use Windows speech until Azure setup is complete.

## 1. Create your Azure Speech resource

1. Open the [Microsoft Azure portal](https://portal.azure.com/) and sign in with your Microsoft account. If you do not have an Azure subscription, follow the account/subscription setup offered there. Azure may require payment verification; check the terms shown during signup.
2. Search for **Speech** in the portal. Create Microsoft's **Speech service** resource (Azure's wording may also mention Foundry). Use a Speech resource with a **resource key and region**; you do not need a custom voice model or a voice-cloning project.
3. Select your subscription. Create a resource group such as **Assistant**, choose an available region such as **East US**, and choose a unique resource name such as **skynet-voice-chris** (use another name if taken).
4. For pricing, choose **Free F0** if offered and suitable for you. Review its limits. If only a paid tier is available, review the price before choosing it. This app does not create resources or change your billing plan for you. See [Azure Speech pricing](https://azure.microsoft.com/en-us/pricing/details/cognitive-services/speech-services/) and [Speech quotas and limits](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/speech-services-quotas-and-limits).
5. Choose **Review + create**, then **Create**. Wait for deployment and choose **Go to resource**.
6. Open **Keys and Endpoint** under the resource's management section. You need **Key 1** (Key 2 also works) and the **Location/Region code**, for example **eastus**. The key and region must belong to the same resource. Do not paste the key into chat, screenshots, or source files.

Microsoft's [text-to-speech quickstart](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/get-started-text-to-speech?pivots=programming-language-rest) and [REST prerequisites](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech#prerequisites) describe the account, Speech resource, and credentials. ASSISTANT handles requests directly, so you do not need to install an SDK, run Microsoft's sample code, or set environment variables.

## 2. Enter the details privately in ASSISTANT

1. Open **Assistant** and click **VOICE SETTINGS** in the left column.
2. Select **Azure natural voices / online** and **Luna / American female / conversational**.
3. In **AZURE SPEECH REGION**, enter the short code such as **eastus**, not “East US” and not an endpoint URL. Use your resource's actual code.
4. Paste **Key 1** into the masked **AZURE SPEECH KEY** box.
5. Click **PREVIEW**. You should hear: “Assistant online.” Preview makes a real Azure request and may count toward usage.
6. If you like it, click **SAVE VOICE**. Try “Assistant, open Notepad” while listening, or use a saved app's manual launch button. The new voice is used for spoken confirmations. The HUD's voice label should show **Luna / Azure natural**.

Preview does not save settings. Save after a successful preview. Try **Jenny / American female**, **Libby / British female**, **Sonia / British female**, **Ryan / British male**, or **Thomas / British male**, and adjust speed/volume as desired. These voices appear in Microsoft's [supported voice list](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts).

## If preview fails

- **Key or region rejected:** copy the key again from the Speech resource, confirm the region code, and check whether key-based access is enabled for that resource. The app uses the public Azure Speech regional endpoint; sovereign-cloud or private-only endpoints are not supported in this build.
- **Limit or service capacity reached:** wait and retry, then check the resource's usage/quota and availability. Repeated Preview clicks make new requests.
- **Could not reach the service:** check internet connectivity and the region. The app does not modify your firewall or resource networking.
- **No sound after a successful preview:** check Windows volume/output device and the voice volume slider.
- **Old Windows voice during launches:** Azure must be saved as the provider. If the HUD says **Windows fallback / online unavailable**, the online request failed and the enabled fallback was used. Open settings and Preview for the error. Disable fallback if you prefer silent confirmations when Azure cannot be reached.

The key is protected on disk with Windows current-user encryption. Spoken confirmations, previews, requested agenda titles/times, commute timing summaries, alerts, and read-aloud ChatGPT responses go to Microsoft for synthesis; microphone recognition and hardware monitoring stay local. Choose **Installed Windows voices / offline** and save to switch back. Clear the masked key before saving Windows mode if you want to remove the saved Azure key.
