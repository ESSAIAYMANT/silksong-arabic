using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SilksongArabic;

// Opt-in observation only: never calls Play, changes volume, or alters dialogue.
internal static class DialogueDiagnostics
{
    static bool Enabled => File.Exists(Path.Combine(Plugin.Folder,"dialogue-audit.flag"));
    static void Record(string text)
    {
        if(!Enabled)return;
        File.AppendAllText(Path.Combine(Plugin.Folder,"dialogue-audit.txt"),
            DateTime.UtcNow.ToString("O")+" "+text+"\n");
    }

    [HarmonyPatch(typeof(NPCControlBase),nameof(NPCControlBase.NewLineStarted))]
    static class LineObserver
    {
        static void Prefix(NPCControlBase __instance,DialogueBox.DialogueLine line)
        {
            if(!Enabled)return;
            Record($"Line npc={__instance.name}; player={line.IsPlayer}; event={line.Event}; Arabic={Plugin.HasArabic(line.Text)}; length={line.Text?.Length}; heroTable={HeroTalkAnimation.TalkAudioTable?.name}");
        }
    }

    [HarmonyPatch(typeof(HeroTalkAnimation),"InternalSetTalking")]
    static class HeroObserver
    {
        static void Prefix(bool setTalking,bool setAnimating,bool ___wasTalking,int ___linesSinceLastSpeak)
        {
            if(!Enabled)return;
            Record($"Hero talking={setTalking}; animating={setAnimating}; previouslyTalking={___wasTalking}; linesSinceVoice={___linesSinceLastSpeak}");
        }
    }

    [HarmonyPatch(typeof(NPCSpeakingAudio),"PlayVoice",new[]{typeof(RandomAudioClipTable),typeof(Vector3),typeof(AudioSource),typeof(NPCSpeakingAudio)})]
    static class VoiceObserver
    {
        static void Postfix(RandomAudioClipTable audioTable,NPCSpeakingAudio runner,AudioSource ____currentPlayingSource)
        {
            if(!Enabled)return;
            var s=____currentPlayingSource;
            Record($"Voice owner={(runner ? runner.name : "Hornet/direct")}; table={audioTable?.name}; clip={s?.clip?.name}; playing={s?.isPlaying}; volume={s?.volume}; mute={s?.mute}; listenerPause={AudioListener.pause}; listenerVolume={AudioListener.volume}");
        }
    }
}
