﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lidgren.Network;
using OpenTK.Audio.OpenAL;

namespace Barotrauma.Networking
{
    class VoipCapture : VoipQueue, IDisposable
    {
        private static VoipCapture instance = null;

        private IntPtr captureDevice;

        private Thread captureThread;

        private bool capturing;

        private short[] uncompressedBuffer;
        
        private VoipCapture(GameClient client) : base(client.ID,true,false) {
            if (instance!=null)
            {
                throw new Exception("Tried to instance more than one VoipCapture object");
            }

            instance = this;

            //set up capture device
            captureDevice = Alc.CaptureOpenDevice(null, VoipConfig.FREQUENCY, ALFormat.Mono16, VoipConfig.BUFFER_SIZE * 5);

            ALError alError = AL.GetError();
            AlcError alcError = Alc.GetError(captureDevice);
            if (alcError != AlcError.NoError)
            {
                throw new Exception("Failed to open capture device: " + alcError.ToString() + " (ALC)");
            }
            if (alError != ALError.NoError)
            {
                throw new Exception("Failed to open capture device: " + alError.ToString() + " (AL)");
            }

            VoipConfig.SetupEncoding();

            capturing = true;
            uncompressedBuffer = new short[VoipConfig.BUFFER_SIZE];
            captureThread = new Thread(UpdateCapture);
            captureThread.Start();
        }

        void UpdateCapture()
        {
            while (!capturing)
            {
                int sampleCount = 0;
                AlcError alcError;
                Alc.GetInteger(captureDevice, AlcGetInteger.CaptureSamples, 1, out sampleCount);
                alcError = Alc.GetError(captureDevice);
                if (alcError != AlcError.NoError)
                {
                    throw new Exception("Failed to determine sample count: " + alcError.ToString());
                }

                if (sampleCount < VoipConfig.BUFFER_SIZE)
                {
                    int sleepMs = (VoipConfig.BUFFER_SIZE - sampleCount) * 800 / VoipConfig.FREQUENCY;
                    if (sleepMs < 5) sleepMs = 5;
                    Thread.Sleep(sleepMs);
                    continue;
                }

                GCHandle handle = GCHandle.Alloc(uncompressedBuffer, GCHandleType.Pinned);
                try
                {
                    Alc.CaptureSamples(captureDevice, handle.AddrOfPinnedObject(), VoipConfig.BUFFER_SIZE);
                }
                finally
                {
                    handle.Free();
                }

                alcError = Alc.GetError(captureDevice);
                if (alcError != AlcError.NoError)
                {
                    throw new Exception("Failed to capture samples: " + alcError.ToString());
                }

                //encode audio and enqueue it
                lock (buffers)
                {
                    int compressedCount = VoipConfig.Encoder.Encode(uncompressedBuffer, 0, VoipConfig.BUFFER_SIZE, BufferToQueue, 0, VoipConfig.MAX_COMPRESSED_SIZE);
                    EnqueueBuffer(compressedCount);
                }

                Thread.Sleep(VoipConfig.BUFFER_SIZE * 800 / VoipConfig.FREQUENCY);
            }
        }

        public override void Write(NetBuffer msg)
        {
            lock (buffers)
            {
                base.Write(msg);
            }
        }

        public override void Read(NetBuffer msg)
        {
            throw new Exception("Called Read on a VoipCapture object");
        }

        public void Dispose()
        {
            instance = null;
        }
    }
}
