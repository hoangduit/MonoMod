﻿using System;
using System.Reflection;
using System.IO;
using Mono.Cecil;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Diagnostics;

namespace MonoMod.JIT {
    /// <summary>
    /// Class that does black magic at runtime.
    /// </summary>
    public class MonoModJIT : MonoMod {

        private readonly Dictionary<MethodDefinition, Delegate> CacheParsed = new Dictionary<MethodDefinition, Delegate>();
        private readonly Dictionary<Type, TypeDefinition> CacheTypeDefs = new Dictionary<Type, TypeDefinition>();
        private readonly Dictionary<MethodInfo, MethodDefinition> CacheMethodDefs = new Dictionary<MethodInfo, MethodDefinition>();

        private byte[] OriginalChecksum;

        public ModuleDefinition OriginalModule;

        public Assembly PatchedAssembly;

        public MonoModJIT(Assembly assembly)
            : base(assembly.CodeBase) {
            Out = new FileInfo(assembly.CodeBase.Substring(0, assembly.CodeBase.Length-4)+".mmc");
        }

        public override void Read(bool loadDependencies = true) {
            base.Read(loadDependencies);

            OriginalModule = Module;

            CheckChecksum();

            if (!Out.Exists) {
                return;
            }

            Module = ModuleDefinition.ReadModule(Out.FullName);

            CheckChecksum();
        }

        public void CheckChecksum() {
            if (OriginalChecksum == null) {
                using (MD5 md5 = MD5.Create()) {
                    using (FileStream stream = File.OpenRead(In.FullName)) {
                        OriginalChecksum = md5.ComputeHash(stream);
                    }
                }
            }

            if (Module == OriginalModule) {
                return;
            }

            //TODO check checksum from wasHere
        }

        public override TypeDefinition PatchWasHere() {
            TypeDefinition wasHere = base.PatchWasHere();

            //TODO add checksum to wasHere

            return wasHere;
        }

        public TypeDefinition GetTypeDefinition(Type type) {
            TypeDefinition def;
            if (CacheTypeDefs.TryGetValue(type, out def)) {
                return def;
            }

            Type highest = type;
            int count = 1;
            while ((highest = highest.DeclaringType) != null) {
                count++;
            }
            Type[] path = new Type[count];
            highest = path[0] = type;
            for (int i = 1; (highest = highest.DeclaringType) != null; i++) {
                path[i] = highest;
            }

            for (int i = 0; i < path.Length; i++) {
                if (def == null) {
                    def = Module.GetType(path[i].FullName);
                    continue;
                }

                for (int ii = 0; ii < def.NestedTypes.Count; ii++) {
                    if (def.NestedTypes[ii].Name == path[i].Name) {
                        //Probably check for more than that
                        def = def.NestedTypes[ii];
                        break;
                    }
                }
            }

            CacheTypeDefs[type] = def;
            return def;
        }

        public MethodDefinition GetMethodDefinition(MethodInfo info) {
            MethodDefinition def;
            if (CacheMethodDefs.TryGetValue(info, out def)) {
                return def;
            }

            TypeDefinition type = GetTypeDefinition(info.DeclaringType);
            for (int i = 0; i < type.Methods.Count; i++) {
                if (type.Methods[i].Name == info.Name && type.Methods[i].Parameters.Count == info.GetParameters().Length) {
                    //Probably check for more than that
                    def = type.Methods[i];
                    break;
                }
            }

            CacheMethodDefs[info] = def;
            return def;
        }

        public MethodDefinition GetPatched(MethodInfo method) {
            return GetPatched(GetMethodDefinition(method));
        }

        public MethodDefinition GetPatched(MethodDefinition method) {
            //TODO
            if (MonoMod.HasAttribute(method, "JIT.MonoModJITPatched")) {
                return method;
            }

            AutoPatch();

            return null;
        }

        public Delegate Parse(MethodInfo method) {
            return Parse(GetMethodDefinition(method));
        }

        public Delegate Parse(MethodDefinition method) {
            method = GetPatched(method);

            Delegate dimd;
            if (CacheParsed.TryGetValue(method, out dimd)) {
                return dimd;
            }

            CacheParsed[method] = dimd;

            return dimd;
        }

    }
}