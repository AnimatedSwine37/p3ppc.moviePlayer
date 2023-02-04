using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Memory.Sources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace p3ppc.cutsceneCaller
{
    public static unsafe class MovieCaller
    {
        private static PlayMovieDelegate _playMovie;
        private static IsMoviePlayingDelegate _isMoviePlaying;
        private static StopMovieDelegate _stopMovie;
        private static IntroStruct _introStruct = new();
        private static nuint _movieThing1;
        private static nuint* _movieThing2;
        private static Input* _input;
        
        private static bool _movieIsPlaying = false;

        internal static void Initialise(IStartupScanner startupScanner, IReloadedHooks hooks)
        {
            var stateInfoAddr = Memory.CurrentProcess.Allocate(sizeof(IntroStateStruct));
            _introStruct.StateInfo = (IntroStateStruct*)stateInfoAddr;
            *_introStruct.StateInfo = new IntroStateStruct();

            startupScanner.AddMainModuleScan("F7 05 ?? ?? ?? ?? 00 60 00 00 75 ??", result =>
            {
                if (!result.Found)
                {
                    Console.WriteLine("Unable to find input2, you won't be able to skip cutscenes.");
                    return;
                }
                Utils.LogDebug($"Found input2 pointer at 0x{result.Offset + Utils.BaseAddress:X}");

                _input = (Input*)(Utils.GetGlobalAddress(result.Offset + Utils.BaseAddress + 2) + 4);
                Utils.LogDebug($"Input2 is at 0x{(nuint)_input:X}");
            });


            startupScanner.AddMainModuleScan("E8 ?? ?? ?? ?? 48 8B 4B ?? E8 ?? ?? ?? ?? 83 F8 01 0F 84 ?? ?? ?? ?? C7 03 06 00 00 00", result =>
            {
                if (!result.Found)
                {
                    Console.WriteLine("Unable to find StopMovie, you won't be able to skip cutscenes.");
                    return;
                }
                Utils.LogDebug($"Found StopMovie call at 0x{result.Offset + Utils.BaseAddress:X}");

                var address = Utils.GetGlobalAddress(result.Offset + Utils.BaseAddress + 1);
                Utils.LogDebug($"Found StopMovie at 0x{address:X}");
                _stopMovie = hooks.CreateWrapper<StopMovieDelegate>((long)address, out _);
            });

            startupScanner.AddMainModuleScan("40 53 48 83 EC 20 48 8B D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 33 C0", result =>
            {
                if (!result.Found)
                {
                    Utils.LogError("Unable to find IsMoviePlaying, won't actually be able to call cutscenes.");
                    return;
                }
                Utils.LogDebug($"Found IsMoviePlaying at 0x{result.Offset + Utils.BaseAddress:X}");

                _isMoviePlaying = hooks.CreateWrapper<IsMoviePlayingDelegate>(result.Offset + Utils.BaseAddress, out _);
            });

            startupScanner.AddMainModuleScan("48 8B 05 ?? ?? ?? ?? 4C 8D 05 ?? ?? ?? ?? 48 89 44 24 ??", result =>
            {
                if (!result.Found)
                {
                    Utils.LogError("Unable to find  movie things, won't actually be able to call cutscenes.");
                    return;
                }
                Utils.LogDebug($"Found movie things at 0x{result.Offset + Utils.BaseAddress:X}");

                _movieThing1 = Utils.GetGlobalAddress(result.Offset + Utils.BaseAddress + 10);
                Utils.LogDebug($"Movie thing 1 is at 0x{_movieThing1:X}");

                _movieThing2 = (nuint*)Utils.GetGlobalAddress(result.Offset + Utils.BaseAddress + 3);
                Utils.LogDebug($"Movie thing 2 is at 0x{(nuint)_movieThing2:X}");
            });
            startupScanner.AddMainModuleScan("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 40 49 89 CE 4C 89 CE", result =>
            {
                if (!result.Found)
                {
                    Utils.LogError($"Unable to find CallMovie, won't actually be able to call cutscenes.");
                    return;
                }
                Utils.LogDebug($"Found CallMovie at 0x{result.Offset + Utils.BaseAddress:X}");

                _playMovie = hooks.CreateWrapper<PlayMovieDelegate>(result.Offset + Utils.BaseAddress, out _);
                SetupFlowFunc(hooks);
            });
        }

        private static void SetupFlowFunc(IReloadedHooks hooks)
        {
            var unusedFunc = FlowUtils.GetFlowFunction(0, 4);
            Utils.LogDebug($"CUSTOM_CALL_CUTSCENE info is at 0x{(nuint)unusedFunc:X}");
            unusedFunc->Function = hooks.Utilities.GetFunctionPointer(typeof(MovieCaller), "CallCutsceneFlowFunc");
            unusedFunc->NumArgs = 1;
        }

        private static int count = 0;
        [UnmanagedCallersOnly]
        public static int CallCutsceneFlowFunc()
        {
            if (_isMoviePlaying == null) return 1;
            
            if (!_movieIsPlaying)
            {
                _movieIsPlaying = true;
                StartCutscene();
                return 0;
            }

            if (!_isMoviePlaying(_introStruct.StateInfo->OperationInfo))
            {
                Utils.LogDebug($"Done playing cutscene");
                _movieIsPlaying = false;
                return 1;
            }

            if ((*_input & Input.Start) != 0)
            {
                Utils.LogDebug($"Skipping cutscene");
                _stopMovie();
                _movieIsPlaying = false;
                return 1;
            }

            return 0;
        }

        private static void StartCutscene()
        {
            int cutscenedId = FlowUtils.GetFlowInput(0);

            string usmPath = $"sound/usm/{cutscenedId}.usm";
            Utils.LogDebug($"Playing {usmPath}");
            var operationInfo = _playMovie(_introStruct, usmPath, _movieThing1, 0, 0, *_movieThing2);
            Utils.LogDebug($"Operation info is at 0x{(nuint)operationInfo:X}");
            // I don't really know what these are but if they aren't 0 the game will crash. I think they are meant to point to something but 0 works so I'll just go with that
            operationInfo->PointerThing1 = 0;
            operationInfo->PointerThing2 = 0; 
            _introStruct.StateInfo->OperationInfo = operationInfo;
        }


        [StructLayout(LayoutKind.Explicit)]
        private struct IntroStruct
        {
            [FieldOffset(72)]
            internal IntroStateStruct* StateInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct IntroStateStruct
        {
            [FieldOffset(0)]
            internal int State;

            [FieldOffset(16)]
            internal TaskStruct* OperationInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct TaskStruct
        {
            [FieldOffset(0x68)]
            internal nuint PointerThing1;

            [FieldOffset(0x78)]
            internal nuint PointerThing2;
        }

        [Function(CallingConventions.Microsoft)]
        private delegate void StopMovieDelegate();

        [Function(CallingConventions.Microsoft)]
        private delegate TaskStruct* PlayMovieDelegate(IntroStruct introStruct, string moviePath, nuint movieThing1, int param4, int param5, nuint movieThing2);

        [Function(CallingConventions.Microsoft)]
        private delegate bool IsMoviePlayingDelegate(TaskStruct* movieInfo);

        private enum Input : ushort
        {
            Start = 8,
            Up = 16,
            Right = 32,
            Down = 64,
            Left = 128,
            RotateLeft = 1280,
            RotateRight = 2560,
            CommandMenu = 4096,
            Confirm = 8192,
            Escape = 16384,
            SubMenu = 32768,
        }

    }
}
