﻿#if (UNITY_IOS && !UNITY_EDITOR) || __IOS__
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Voice = Photon.Voice;

namespace Photon.Voice.IOS
{
    public class MonoPInvokeCallbackAttribute : System.Attribute
    {
        private Type type;
        public MonoPInvokeCallbackAttribute(Type t) { type = t; }
    }

    public class AudioInPusher : Voice.IAudioPusher<float>
    {
        const string lib_name = "__Internal";
        [DllImport(lib_name)]
        private static extern IntPtr Photon_Audio_In_CreatePusher(int instanceID, Action<int, IntPtr, int> pushCallback, int sessionCategory, int sessionMode, int sessionCategoryOptions);
        [DllImport(lib_name)]
        private static extern void Photon_Audio_In_Destroy(IntPtr handler);

        private delegate void CallbackDelegate(int instanceID, IntPtr buf, int len);

        public AudioInPusher(AudioSessionParameters sessParam, Voice.ILogger logger)
        {
            try
            {
                handle = Photon_Audio_In_CreatePusher(instanceCnt, nativePushCallback, (int)sessParam.Category, (int)sessParam.Mode, sessParam.CategotyOptionsToInt());
                instancePerHandle.Add(instanceCnt++, this);
            }
            catch (Exception e)
            {
                Error = e.ToString();
                if (Error == null) // should never happen but since Error used as validity flag, make sure that it's not null
                {
                    Error = "Exception in AudioInPusher constructor";
                }
                logger.LogError("[PV] AudioInPusher: " + Error);
            }
        }

        // IL2CPP does not support marshaling delegates that point to instance methods to native code.
        // Using static method and per instance table.
        static int instanceCnt;
        private static Dictionary<int, AudioInPusher> instancePerHandle = new Dictionary<int, AudioInPusher>();
        [MonoPInvokeCallbackAttribute(typeof(CallbackDelegate))]
        private static void nativePushCallback(int instanceID, IntPtr buf, int len)
        {
            AudioInPusher instance;
            if (instancePerHandle.TryGetValue(instanceID, out instance))
            {
                instance.push(buf, len);
            }
        }

        IntPtr handle;
        Action<float[]> pushCallback;
        Voice.ObjectFactory<float[], int> bufferFactory;

        // Supposed to be called once at voice initialization.
        // Otherwise recreate native object (instead of adding 'set callback' method to native interface)
        public void SetCallback(Action<float[]> callback, Voice.ObjectFactory<float[], int> bufferFactory)
        {
            this.pushCallback = callback;
            this.bufferFactory = bufferFactory;
        }
        private void push(IntPtr buf, int len)
        {            
            var bufManaged = bufferFactory.New(len);
            Marshal.Copy(buf, bufManaged, 0, len);
            pushCallback(bufManaged);
        }

        public int Channels { get { return 1; } }

        public int SamplingRate { get { return 48000; } }

        public string Error { get; private set; }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                Photon_Audio_In_Destroy(handle);
            }
            // TODO: Remove this from instancePerHandle
        }
    }
}
#endif