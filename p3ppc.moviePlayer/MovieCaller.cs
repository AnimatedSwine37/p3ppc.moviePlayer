using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
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

namespace p3ppc.movieCaller
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
        private static IHook<OpenBacklogDelegate> _openBacklogHook;
        private static IAsmHook _closeBacklogHook;
        private static IReverseWrapper<MovieIsPlayingDelegate> _movieIsPlayingReverseWrapper;
        
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
                    Console.WriteLine("Unable to find input2, you won't be able to skip movies.");
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
                    Console.WriteLine("Unable to find StopMovie, you won't be able to skip movies.");
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
                    Utils.LogError("Unable to find IsMoviePlaying, won't actually be able to call movies.");
                    return;
                }
                Utils.LogDebug($"Found IsMoviePlaying at 0x{result.Offset + Utils.BaseAddress:X}");

                _isMoviePlaying = hooks.CreateWrapper<IsMoviePlayingDelegate>(result.Offset + Utils.BaseAddress, out _);
            });

            startupScanner.AddMainModuleScan("48 8B 05 ?? ?? ?? ?? 4C 8D 05 ?? ?? ?? ?? 48 89 44 24 ??", result =>
            {
                if (!result.Found)
                {
                    Utils.LogError("Unable to find  movie things, won't actually be able to call movies.");
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
                    Utils.LogError($"Unable to find CallMovie, won't actually be able to call movies.");
                    return;
                }
                Utils.LogDebug($"Found CallMovie at 0x{result.Offset + Utils.BaseAddress:X}");

                _playMovie = hooks.CreateWrapper<PlayMovieDelegate>(result.Offset + Utils.BaseAddress, out _);
                SetupFlowFunc(hooks);
            });

            startupScanner.AddMainModuleScan("48 83 EC 28 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 85 C0 75 ?? 31 C9", result =>
            {
                if (!result.Found)
                {
                    Utils.LogError($"Unable to find OpenBacklog, the backlog can be opened during movies.");
                    return;
                }
                Utils.LogDebug($"Found OpenBacklog at 0x{result.Offset + Utils.BaseAddress:X}");

                _openBacklogHook = hooks.CreateHook<OpenBacklogDelegate>(OpenBacklog, Utils.BaseAddress + result.Offset).Activate();
            });

            startupScanner.AddMainModuleScan("A8 08 75 ?? 0F BA E0 0E", result =>
            {
                if (!result.Found)
                {
                    Utils.LogError($"Unable to find CloseBacklog, the backlog won't be closed automatically if a movie starts.");
                    return;
                }
                Utils.LogDebug($"Found CloseBacklog at 0x{result.Offset + Utils.BaseAddress:X}");

                string[] function =
                {
                    "use64",
                    "push rax",
                    "push rcx\npush rdx\npush r8\npush r9\npush r10\npush r11",
                    "sub rsp, 40",
                    $"{hooks.Utilities.GetAbsoluteCallMnemonics(MovieIsPlaying, out _movieIsPlayingReverseWrapper)}",
                    "add rsp, 40",
                    "pop r11\npop r10\npop r9\npop r8\npop rdx\npop rcx",
                    "cmp eax, 1",
                    "jne normal",
                    "pop rax",
                    "mov al, 8",
                    "jmp endHook",
                    "label normal",
                    "pop rax",
                    "label endHook"
                };

                _closeBacklogHook = hooks.CreateAsmHook(function, result.Offset + Utils.BaseAddress, AsmHookBehaviour.ExecuteFirst).Activate();
            });
        }

        private static void SetupFlowFunc(IReloadedHooks hooks)
        {
            var unusedFunc = FlowUtils.GetFlowFunction(0, 4);
            Utils.LogDebug($"CUSTOM_CALL_movie info is at 0x{(nuint)unusedFunc:X}");
            unusedFunc->Function = hooks.Utilities.GetFunctionPointer(typeof(MovieCaller), "CallMovieFlowFunc");
            unusedFunc->NumArgs = 1;
        }

        private static int count = 0;
        [UnmanagedCallersOnly]
        public static int CallMovieFlowFunc()
        {
            if (_isMoviePlaying == null) return 1;
            
            if (!_movieIsPlaying)
            {
                _movieIsPlaying = true;
                Startmovie();
                return 0;
            }

            if (!_isMoviePlaying(_introStruct.StateInfo->OperationInfo))
            {
                Utils.LogDebug($"Done playing movie");
                _movieIsPlaying = false;
                return 1;
            }

            if ((*_input & Input.Start) != 0)
            {
                Utils.LogDebug($"Skipping movie");
                _stopMovie();
                _movieIsPlaying = false;
                return 1;
            }

            return 0;
        }

        private static void Startmovie()
        {
            int moviedId = FlowUtils.GetFlowInput(0);

            string usmPath = $"sound/usm/{moviedId}.usm";
            Utils.LogDebug($"Playing {usmPath}");
            var operationInfo = _playMovie(_introStruct, usmPath, _movieThing1, 0, 0, *_movieThing2);
            Utils.LogDebug($"Operation info is at 0x{(nuint)operationInfo:X}");
            // I don't really know what these are but if they aren't 0 the game will crash. I think they are meant to point to something but 0 works so I'll just go with that
            operationInfo->PointerThing1 = 0;
            operationInfo->PointerThing2 = 0; 
            _introStruct.StateInfo->OperationInfo = operationInfo;
        }

        private static void OpenBacklog()
        {
            // If a movie is playing just do nothing
            if (!_movieIsPlaying)
                _openBacklogHook.OriginalFunction();
        }

        private static bool MovieIsPlaying()
        {
            return _movieIsPlaying;
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

        private delegate bool MovieIsPlayingDelegate();

        [Function(CallingConventions.Microsoft)]
        private delegate void StopMovieDelegate();

        [Function(CallingConventions.Microsoft)]
        private delegate TaskStruct* PlayMovieDelegate(IntroStruct introStruct, string moviePath, nuint movieThing1, int param4, int param5, nuint movieThing2);

        [Function(CallingConventions.Microsoft)]
        private delegate bool IsMoviePlayingDelegate(TaskStruct* movieInfo);

        [Function(CallingConventions.Microsoft)]
        private delegate void OpenBacklogDelegate();

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
