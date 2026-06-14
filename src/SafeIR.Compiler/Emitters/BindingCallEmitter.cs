namespace SafeIR.Compiler.Emitters;

using System.Reflection.Emit;
using SafeIR;
using SafeIR.Runtime;
using static SafeIR.Compiler.IlEmitterPrimitives;

internal static class BindingCallEmitter
{
    public static bool TryEmit(
        CallExpression call,
        IBindingCatalog bindings,
        ILGenerator il,
        Action<Expression> emitExpression)
    {
        if (!bindings.TryGet(call.Name, out var binding) || !CanEmitCompiledBinding(binding))
        {
            return false;
        }

        if (CanEmitDirectRuntimeMethod(binding))
        {
            var locals = new LocalBuilder[call.Arguments.Count];
            for (var i = 0; i < call.Arguments.Count; i++)
            {
                emitExpression(call.Arguments[i]);
                locals[i] = il.DeclareLocal(typeof(SandboxValue));
                il.Emit(OpCodes.Stloc, locals[i]);
            }

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, call.Name);
            il.Emit(OpCodes.Call, Runtime(nameof(CompiledRuntime.ChargeBindingCall)));
            foreach (var local in locals)
            {
                il.Emit(OpCodes.Ldloc, local);
            }

            il.Emit(OpCodes.Call, Runtime(binding.Compiled.Method));
            return true;
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldstr, call.Name);
        ValueArrayEmitter.Emit(il, call.Arguments, emitExpression);
        il.Emit(OpCodes.Call, Runtime(nameof(CompiledRuntime.CallBinding)));
        return true;
    }

    private static bool CanEmitCompiledBinding(BindingSignature binding)
        => CanEmitGenericRuntimeStub(binding) || CanEmitDirectRuntimeMethod(binding);

    private static bool CanEmitGenericRuntimeStub(BindingSignature binding)
        => binding.Compiled.Kind == "RuntimeStub" &&
           binding.Compiled.Type == typeof(CompiledRuntime).FullName &&
           binding.Compiled.Method == nameof(CompiledRuntime.CallBinding);

    private static bool CanEmitDirectRuntimeMethod(BindingSignature binding)
        => binding.Compiled.Kind == "RuntimeStub" &&
           binding.Compiled.Type == typeof(CompiledRuntime).FullName &&
           binding.Compiled.Method != nameof(CompiledRuntime.CallBinding) &&
           binding.RequiredCapability is null &&
           binding.Safety == BindingSafety.PureIntrinsic &&
           (binding.Effects & ~(SandboxEffect.Cpu | SandboxEffect.Alloc)) == SandboxEffect.None &&
           binding.AuditLevel == AuditLevel.None;
}
