using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p3ppc.cutsceneCaller
{
    internal unsafe class FlowUtils
    {
        internal struct FlowFunctionGroup
        {
            internal FlowFunction* Functions;
            internal int NumFunctions;
            internal int Unk;
        }

        internal struct FlowFunction
        {
            internal void* Function;
            internal int NumArgs;
            internal int Unk;
        }

        private static FlowFunctionGroup* _flowFunctionGroups;
        private static GetFlowInputDelegate _getFlowInput;

        internal static void Initialise(IStartupScanner startupScanner, IReloadedHooks hooks)
        {
            startupScanner.AddMainModuleScan("48 8D 2D ?? ?? ?? ?? 48 C1 F8 0C", result =>
            {
                if(!result.Found)
                {
                    Utils.LogError("Unable to find flow functions group, unable to hook flow functions.");
                    return;
                }
                Utils.LogDebug($"Found flow function groups pointer at 0x{result.Offset + Utils.BaseAddress:X}");
                
                _flowFunctionGroups =  (FlowFunctionGroup*)Utils.GetGlobalAddress(result.Offset + Utils.BaseAddress + 3);

                Utils.LogDebug($"Found flow function groups at 0x{(nuint)_flowFunctionGroups:X}");
            });

            startupScanner.AddMainModuleScan("48 89 5C 24 ?? 57 48 83 EC 20 8B D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C 8B 05 ?? ?? ?? ??", result =>
            {
                if(!result.Found)
                {
                    Utils.LogError("Unable to find GetFlowInput, input will always be considered 0.");
                    return;
                }
                Utils.LogDebug($"Found GetFlowInput at 0x{result.Offset + Utils.BaseAddress:X}");

                _getFlowInput = hooks.CreateWrapper<GetFlowInputDelegate>(result.Offset + Utils.BaseAddress, out _);
            });
        }

        internal static FlowFunction* GetFlowFunction(int groupIndex, int indexInGroup)
        {
            var group = _flowFunctionGroups[groupIndex];
            if (indexInGroup > group.NumFunctions)
                return null;

            return &group.Functions[indexInGroup];
        }

        internal static int GetFlowInput(int inputIndex)
        {
            return (int)_getFlowInput(inputIndex);
        }

        [Function(CallingConventions.Microsoft)]
        private delegate long GetFlowInputDelegate(int inputIndex);
    }
}
