using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.PE;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace UnmanagedString___Deobfuscator
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            string path = args.Length > 0 ? args[0].Trim('"') : "";

            while (!File.Exists(path))
            {
                Console.Write("File path: ");
                path = Console.ReadLine().Trim('"');
                Console.Clear();
            }

            string outPath = path.Insert(path.Length - 4, "-clean");

            ModuleDefMD module = ModuleDefMD.Load(path);
            IPEImage pe = module.Metadata.PEImage;

            bool isX86 = module.Machine == Machine.I386;
            int prefixLen = isX86 ? 19 : 8;

            int restored = 0;

            foreach (TypeDef type in module.GetTypes())
            {
                foreach (MethodDef method in type.Methods.Where(m => m.HasBody))
                {
                    var instrs = method.Body.Instructions;

                    for (int i = 0; i < instrs.Count - 1; i++)
                    {
                        if (instrs[i].OpCode != OpCodes.Call)
                            continue;

                        MethodDef native = instrs[i].Operand as MethodDef;
                        if (native == null || !native.IsNative || native.RVA == 0)
                            continue;

                        if (instrs[i + 1].OpCode != OpCodes.Newobj)
                            continue;

                        IMethod ctor = instrs[i + 1].Operand as IMethod;
                        if (ctor == null || ctor.DeclaringType.FullName != "System.String")
                            continue;

                        bool unicode = ctor.MethodSig.Params.Any(p => p.ElementType == ElementType.Char);

                        RVA strRva = (RVA)(native.RVA + (uint)prefixLen);
                        string value = ReadString(pe, strRva, unicode);

                        instrs[i].OpCode = OpCodes.Ldstr;
                        instrs[i].Operand = value;

                        instrs.RemoveAt(i + 1);

                        if (i + 1 < instrs.Count && instrs[i + 1].IsLdcI4())
                            instrs.RemoveAt(i + 1);
                        if (i + 1 < instrs.Count && instrs[i + 1].IsLdcI4())
                            instrs.RemoveAt(i + 1);

                        restored++;
                    }

                    if (restored > 0)
                    {
                        method.Body.SimplifyBranches();
                        method.Body.OptimizeBranches();
                    }
                }
            }

            foreach (TypeDef type in module.GetTypes())
            {
                for (int i = type.Methods.Count - 1; i >= 0; i--)
                {
                    MethodDef m = type.Methods[i];
                    if (m.IsNative || m.IsUnmanaged || m.IsPinvokeImpl || (!m.HasBody && m.RVA != 0))
                        type.Methods.RemoveAt(i);
                }
            }

            for (int i = module.GlobalType.Methods.Count - 1; i >= 0; i--)
            {
                MethodDef m = module.GlobalType.Methods[i];
                if (!m.HasBody || m.IsNative || m.IsPinvokeImpl)
                    module.GlobalType.Methods.RemoveAt(i);
            }

            module.Cor20HeaderFlags |= ComImageFlags.ILOnly;
            module.Cor20HeaderFlags &= ~ComImageFlags.Bit32Required;

            module.Write(outPath);

            Console.WriteLine("Restored strings: " + restored);
            Console.WriteLine("Output: " + Path.GetFileName(outPath));
            Console.ReadLine();
        }

        private static string ReadString(IPEImage pe, RVA rva, bool unicode)
        {
            var reader = pe.CreateReader(rva);

            if (unicode)
            {
                MemoryStream ms = new MemoryStream();
                while (true)
                {
                    ushort c = reader.ReadUInt16();
                    if (c == 0)
                        break;
                    ms.WriteByte((byte)(c & 0xFF));
                    ms.WriteByte((byte)(c >> 8));
                }
                return Encoding.Unicode.GetString(ms.ToArray());
            }
            else
            {
                MemoryStream ms = new MemoryStream();
                while (true)
                {
                    byte b = reader.ReadByte();
                    if (b == 0)
                        break;
                    ms.WriteByte(b);
                }
                return Encoding.ASCII.GetString(ms.ToArray());
            }
        }
    }
}
