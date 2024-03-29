﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;

public class SampleManager : MonoBehaviour
{

   #region Static Properties
   public static SampleManager Instance { get; set; }
   #endregion

   public enum OriginType
   {
      Default,
      Mix,
      Mic,
      None
   }

   // TODO: change this to relate to intent of mixload
   // MixLoadIntent.Restore
   // MixLoadIntent.New
   public static bool inAsyncRestore = false;

   public int maxChannels = 16;
   public int numChannels = 15;
   public bool editMode;
   public AudioClip[] soundClips;
   public OriginType[] soundOrigins;
   public string lastMixToken = null;

   private AudioSource[] soundSources;
   private GameObject[] buttonGOs;
   private bool inRecord = false;
   private int channelInRecord = -1;
   private Image imageInRecord = null;
   private double recordStartTime = 0f;
   private double recordEndTime = 0f;
   private double recordMaxDuration = 6f;

   // is there a connected microphone?
   private bool micConnected = false;

   private bool[] isPlayed = { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false };
   private bool lightChange = false;
   private int prevIndex = -1, curIndex = -1;

   // maximum and minimum available recording frequencies  
   private int minFreq;
   private int maxFreq;
   private int maxRecordSec = 6;
   private string[] defaultSoundAssetnames = new string[15] {
      "drums/1001",
      "drums/1222",
      "drums/1012",
      "drums/1003",
      "drums/1307",
      "drums/1308",
      "drums/1038",
      "drums/1078",
      "drums/1008",
      "drums/1037",
      "drums/1047",
      "drums/1172",
      "drums/1002",
      "drums/1001",
      "drums/1222",
   };
   private Dictionary<string, object> localState = new Dictionary<string, object>();
   private string stateKey = "soundState";

   void Awake()
   {
      if (SampleManager.Instance == null)
         SampleManager.Instance = this;
   }

   // Use this for initialization
   void Start()
   {

      soundClips = new AudioClip[numChannels];
      soundSources = new AudioSource[numChannels];
      soundOrigins = new OriginType[numChannels];
      buttonGOs = new GameObject[numChannels];

      // initialize default audio sources
      for (int i = 0; i < numChannels; i++)
      {
         GameObject child = new GameObject("Sound" + i);
         child.transform.parent = gameObject.transform;
         soundSources[i] = child.AddComponent<AudioSource>() as AudioSource;

         if (defaultSoundAssetnames.Length >= (i + 1))
         {
            soundSources[i].clip = Resources.Load(defaultSoundAssetnames[i], typeof(AudioClip)) as AudioClip;
            soundOrigins[i] = OriginType.Default;
         }
         else
         {
            soundOrigins[i] = OriginType.None;
         }
      }

      editMode = false;

      // get refs to the GOs for each sample button
      for (int i = 0; i < numChannels; i++)
      {
         GameObject go = GameObject.Find("SampleButton" + i);
         buttonGOs[i] = go;
      }

      if (!MicCheck())
      {
         // TODO: handle the case where there is no mic
      }

      // restore prev state of samples
      RestoreState();

      InvokeRepeating("GetInputsFromMQTT", 0.1f, 0.1f);
   }

   // public method to revert to default sample state
   public void ResetSamples()
   {
      SetStateFromDefault();
   }

   // initialize default audio sources for all
   private void SetStateFromDefault()
   {
      for (int i = 0; i < numChannels; i++)
      {
         SetSlotFromDefault(i);
      }
   }

   // initialize one default audio source
   private void SetSlotFromDefault(int i)
   {
      if (defaultSoundAssetnames.Length >= (i + 1))
      {
         soundSources[i].clip = Resources.Load(defaultSoundAssetnames[i], typeof(AudioClip)) as AudioClip;
         soundOrigins[i] = OriginType.Default;
         SaveSlot(i, defaultSoundAssetnames[i], "default", defaultSoundAssetnames[i]);
      }
      else
      {
         // blank out the clip
         soundSources[i].clip = null;
         soundOrigins[i] = OriginType.None;
      }
   }

   // initialize one audio source from mic recording
   private void SetSlotFromMic(string filename, int i)
   {
      if (numChannels >= (i + 1))
      {
         string url = "file://" + Application.persistentDataPath + "/" + filename + ".wav";
         StartCoroutine(SetClipFromUrl(url, i));
      }
      else
      {
         // blank out the clip
         soundSources[i].clip = null;
         soundOrigins[i] = OriginType.None;
      }
   }

   private void RestoreState()
   {
      if (String.IsNullOrEmpty(PlayerPrefs.GetString(stateKey)))
      {
         SetStateFromDefault();
         return;
      }

      RestoreStateSync();
   }

   // restore audio clips from available sources
   private void RestoreStateSync()
   {
      var stateInfo = MiniJSON.Json.Deserialize(PlayerPrefs.GetString(stateKey)) as Dictionary<string, object>;
      for (int i = 0; i < numChannels; i++)
      {
         if (stateInfo.ContainsKey(i.ToString()) == false)
         {
            SetSlotFromDefault(i);
            continue;
         }

         var tmpSlot = stateInfo[i.ToString()] as Dictionary<string, object>;
         var tmpType = tmpSlot["type"] as string;
         var tmpSrc = tmpSlot["src"] as string;

         localState[i.ToString()] = tmpSlot;

         if (tmpType == "default")
         {
            SetSlotFromDefault(i);
         }
         else if (tmpType == "mic")
         {
            SetSlotFromMic(tmpSrc, i);
         }
      }
      inAsyncRestore = false;
   }

   private void SaveSlot(int i, string name, string type, string src)
   {
      var slot = new Dictionary<string, object>();
      slot.Add("name", name);
      slot.Add("type", type);
      slot.Add("src", src);
      localState[i.ToString()] = slot;
      SaveState(localState);
   }

   private void SaveState(Dictionary<string, object> stateObj)
   {
      PlayerPrefs.SetString(stateKey, MiniJSON.Json.Serialize(stateObj));
   }

   public Dictionary<string, object> GetState()
   {
      var stateInfo = MiniJSON.Json.Deserialize(PlayerPrefs.GetString(stateKey)) as Dictionary<string, object>;
      return stateInfo;
   }

   // tidy up when it's time to stop recording
   public void FinishRecording()
   {
      if (!inRecord)
         return;

      Microphone.End(null); // stop the audio recording

      string tmpName = "s" + channelInRecord;
      AudioHelper.Save(tmpName, soundSources[channelInRecord].clip);

      SaveSlot(channelInRecord, tmpName, "mic", tmpName);

      // update visual state
      Component[] groups = buttonGOs[channelInRecord].GetComponentsInChildren<CanvasGroup>();
      foreach (CanvasGroup group in groups)
      {
         group.alpha = 0f;
      }
      imageInRecord.fillAmount = 0f;

      // update flags
      inRecord = false;
      channelInRecord = -1;
      imageInRecord = null;

      recordStartTime = 0f;
      recordEndTime = 0f;
   }

   // handles taps on all sample buttons
   public void PointerDown(int channel)
   {
      // if not in editing mode, play the sample
      if (!editMode)
      {
         PlaySample(channel, AudioSettings.dspTime);
         return;
      }

      // if currently recording, finish up
      if (inRecord)
      {
         FinishRecording();
         DisplayManager.Instance.ToggleRecordingState("stopped");
         return;
      }

      // handle recording capabilities
      if (!micConnected)
      { // TODO: handle no mic case
         Debug.Log("Error: no mic!");
         return;
      }

      // start recording; audio captured in AudioClip
      Debug.Log("starting to record channel " + channel + " ...");
      soundSources[channel].clip = Microphone.Start(null, true, maxRecordSec, maxFreq);
      DisplayManager.Instance.ToggleRecordingState("started");

      // update visual state
      Transform circleTransform = buttonGOs[channel].transform.Find("ProgressCircle");
      imageInRecord = circleTransform.gameObject.GetComponent<Image>();
      imageInRecord.fillAmount = 0.23f;

      Component[] groups = buttonGOs[channel].GetComponentsInChildren<CanvasGroup>();
      foreach (CanvasGroup group in groups)
      {
         group.alpha = 0.7f;
      }

      // update flags
      inRecord = true;
      channelInRecord = channel;

      // record start and max end time
      recordStartTime = AudioSettings.dspTime;
      recordEndTime = recordStartTime + Convert.ToDouble(maxRecordSec);
   }

   public void PointerUp(int channel)
   {
      // Debug.Log("Sample " + channel + " Pointer is up");
   }

   public void AssignClipToChannel(AudioClip clip, int channel)
   {
      soundClips[channel] = clip; // save the clip
      soundSources[channel].clip = clip;
      // TODO: update state
   }

   // wrapper for putting url/file ref into a channel
   public void AssignUrlToChannel(string url, int channel)
   {
      // StartCoroutine(SetClipFromUrl(url, soundSources[channel]));
   }

   // get audio data from url and assign to source
   IEnumerator SetClipFromUrl(string url, int channel)
   {
      WWW localFile = new WWW(url);
      yield return localFile;

      if (localFile.error == null)
      {
         Debug.Log("Loaded from url: OK");
      }
      else
      {
         Debug.Log("Loaded from url: error: " + localFile.error);
         yield break;
      }

      soundClips[channel] = localFile.GetAudioClip(false, false); // save the clip
      soundSources[channel].clip = soundClips[channel];
      Debug.Log("Set sample " + channel + " from mic recording");
   }

   public void PlaySample(int channel, double playTime)
   {
      soundSources[channel].PlayScheduled(playTime);
   }

   public void PlayDelayed(int channel, float delay)
   {
      soundSources[channel].PlayDelayed(delay);
   }

   public void StopSample(int channel)
   {
      soundSources[channel].Stop();
   }

   public void StopAll()
   {
      for (int i = 0; i < numChannels; i++)
      {
         soundSources[i].Stop();
      }
   }

   public bool MicCheck()
   {
      // check if there is at least one microphone connected  
      if (Microphone.devices.Length <= 0)
      {
         // TODO: throw a warning message at the console if there isn't  
         Debug.Log("Microphone not connected!");
      }
      else
      { // at least one microphone is present  
         // set 'micConnected' to true  
         micConnected = true;

         // get the default microphone recording capabilities  
         Microphone.GetDeviceCaps(null, out minFreq, out maxFreq);

         // according to the documentation, if minFreq and maxFreq are zero, the microphone supports any frequency...  
         if (minFreq == 0 && maxFreq == 0)
         {
            // .. .meaning 44100 Hz can be used as the recording sampling rate  
            maxFreq = 44100;
         }
         else if (maxFreq > 44100)
         {
            maxFreq = 44100;
         }
      }

      return micConnected;
   }

   // Update is called once per frame
   void Update()
   {
      // GetInputsFromMQTT ();

      if (!inRecord)
         return;

      // compute fill ratio
      double fillRatio = 1f - (recordEndTime - AudioSettings.dspTime) / recordMaxDuration;

      // check for timeout
      if (fillRatio > 1f)
      {
         FinishRecording();
         return;
      }

      // update any progress bars
      imageInRecord.fillAmount = (float)Math.Min(1f, Math.Max(fillRatio, 0f));
   }

   void GetInputsFromMQTT()
   {
      for (int i = 0; i < Global.datas.Length; i++)
      {
         if (Global.datas[i] == 1)
         {
            if (!isPlayed[i])
            {
               buttonGOs[i].GetComponent<Image>().color = new Color(0.5019608f, 1f, 0.5019608f, 0.7f);
               PointerDown(i);
               isPlayed[i] = true;
               lightChange = true;
               prevIndex = curIndex;
               curIndex = i;
               if (prevIndex != curIndex)
               {
                  switch (i)
                  {
                     case 0: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=1101056")); break;
                     case 1: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=16724224")); break;
                     case 2: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=4589")); break;
                     case 3: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=16744431")); break;
                     case 4: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=8134400")); break;
                     case 5: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=16380160")); break;
                     case 6: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=12124001")); break;
                     case 7: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=15246079")); break;
                     case 8: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=1238")); break;
                     case 9: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=15925163")); break;
                     case 10: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=7602150")); break;
                     case 11: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=2031371")); break;
                     case 12: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=40710")); break;
                     case 13: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=16748533")); break;
                     case 14: StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=28&c=15045")); break;
                  }
               }

               StartCoroutine(Delay(i));
            }
         }
         else
         {
            isPlayed[i] = false;
            buttonGOs[i].GetComponent<Image>().color = new Color(0.003921569f, 0.5921569f, 0.6862745f, 0.627451f);
            if (lightChange)
            {
               StartCoroutine(Delay2(1,()=> {
                  StartCoroutine(ChangeLEDMode("http://192.168.0.123/set?m=8&c=16777215"));
               }));
               lightChange = false;
            }
         }
      }
   }

   IEnumerator Delay(int i)
   {
      yield return new WaitForSeconds(0.05f);
      isPlayed[i] = false;
   }

   IEnumerator Delay2(int val, Action callback)
   {
      yield return new WaitForSeconds(val);
      callback.Invoke();
   }

   IEnumerator ChangeLEDMode(string uri)
   {
      using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
      {
         // Request and wait for the desired page.
         yield return webRequest.SendWebRequest();

         string[] pages = uri.Split('/');
         int page = pages.Length - 1;

         if (webRequest.isNetworkError)
         {
            Debug.Log(pages[page] + ": Error: " + webRequest.error);
         }
         else
         {
            Debug.Log(pages[page] + ":\nReceived: " + webRequest.downloadHandler.text);
         }
      }
   }
}