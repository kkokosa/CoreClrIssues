using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace DynamicMethodInlining
{
    /**
     Sharplab: https://sharplab.io/#v2:EYLgxg9gTgpgtADwGwBYA0AXEBDAzgWwB8ABAJgEYBYAKGIGYACMhgYQYG8aHunHjykDAJYA7DAwCyMDAAsIAEwCCAClHiEASgZcenajwNMA7JOlz5AIWUI0DcgAZHGgNw7uAXzfb9PekwHCYqayClZqDDaB4gCeGl56hr4mCAwAVBEMANQM0a4+HjTuQA==
     
     C# code like follows:

     public class C {
        public static int MethodA(int x) 
        {
            return MethodB(x, 1000);
        }
    
        public static int MethodB(int x, int y)
        {
            return x * x + y;
        }

     is being translated into the following IL:

        .method public hidebysig static 
            int32 MethodA (
                int32 x
            ) cil managed 
        {
            // Method begins at RVA 0x2050
            // Code size 12 (0xc)
            .maxstack 8

            IL_0000: ldarg.0
            IL_0001: ldc.i4 1000
            IL_0006: call int32 C::MethodB(int32, int32)
            IL_000b: ret
        } // end of method C::MethodA

        .method public hidebysig static 
            int32 MethodB (
                int32 x,
                int32 y
            ) cil managed 
        {
            // Method begins at RVA 0x205d
            // Code size 6 (0x6)
            .maxstack 8

            IL_0000: ldarg.0
            IL_0001: ldarg.0
            IL_0002: mul
            IL_0003: ldarg.1
            IL_0004: add
            IL_0005: ret
        } // end of method C::MethodB
    }     
    
    and then MethodB is being inlined by the JIT to:

        C.MethodA(Int32)
            L0000: mov eax, ecx
            L0002: imul eax, ecx
            L0005: add eax, 0x3e8
            L000a: ret

    However the exact same IL dynamically generated into DynamicMethods is not:

    0:015> !u 00007ff9`c50b0080
    Normal JIT generated code
    DynamicClass.(Int32)
    Begin 00007FF9C50B0080, size 12
    00007ff9`c50b0080 bae8030000      mov     edx,3E8h
    00007ff9`c50b0085 48b87052fbc4f97f0000 mov rax,7FF9C4FB5270h
    00007ff9`c50b008f 48ffe0          jmp     rax

    0:015> !u 00007ff9`c50b00f0
    Normal JIT generated code
    DynamicClass.(Int32, Int32)
    Begin 00007FF9C50B00F0, size 8
    00007ff9`c50b00f0 8bc1            mov     eax,ecx
    00007ff9`c50b00f2 0fafc1          imul    eax,ecx
    00007ff9`c50b00f5 03c2            add     eax,edx
    00007ff9`c50b00f7 c3              ret
     */
    class Program
    {
        public delegate int MethodADelegate(int x);
        public delegate int MethodBDelegate(int x, int y);

        static void Main(string[] args)
        {
            var methodB = EmitMethodB();
            var methodA = EmitMethodA(methodB);

            var delegateA = (MethodADelegate) methodA.CreateDelegate(typeof(MethodADelegate));
            var delegateB = (MethodBDelegate) methodB.CreateDelegate(typeof(MethodBDelegate));

            Console.WriteLine(delegateA(5));

            var pointerA = Marshal.GetFunctionPointerForDelegate(delegateA);
            var pointerB = Marshal.GetFunctionPointerForDelegate(delegateB);
            Console.WriteLine($"PointerA: {pointerA.ToInt64():x16}");
            Console.WriteLine($"PointerB: {pointerB.ToInt64():x16}");
            Console.ReadLine();

            // To analyze in WinDbg stop here and u the PointerA (and PointerB) values:
            // > u 00007ff9c4e73144 L1
            // 00007ff9`c4e73144 49ba2031e7c4f97f0000 mov r10,7FF9C4E73120h
            // > dp 7FF9C4E73120h L1
            // 00007ff9`c4e73120  00007ff9`c4fb5268
            // > u 00007ff9`c4fb5268 L1
            // 00007ff9`c4fb5268 e913ae0f00      jmp     00007ff9`c50b0080
            // > !u 00007ff9`c50b0080
        }

        private static DynamicMethod EmitMethodA(DynamicMethod secondMethod)
        {
            DynamicMethod code = new DynamicMethod(string.Empty, typeof(int), new[] { typeof(int) });
            var ilGen = code.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldc_I4, 1000);
            ilGen.EmitCall(OpCodes.Call, secondMethod, null);
            ilGen.Emit(OpCodes.Ret);
            return code;
        }

        private static DynamicMethod EmitMethodB()
        {
            DynamicMethod code = new DynamicMethod(string.Empty, typeof(int), new [] {typeof(int), typeof(int)});
            var ilGen = code.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Dup);
            ilGen.Emit(OpCodes.Mul);
            ilGen.Emit(OpCodes.Ldarg_1);
            ilGen.Emit(OpCodes.Add);
            ilGen.Emit(OpCodes.Ret);
            return code;
        }
    }
}
