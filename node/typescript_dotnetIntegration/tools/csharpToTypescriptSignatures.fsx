// skia-to-ts.fsx — F# Interactive script that emits a SINGLE .d.ts mapping ALL public C# types
// from one or more input assemblies (e.g., SkiaSharp.dll). Designed to run in F# Interactive.
//
// Usage examples (FSI):
//   > #r "System.Runtime";; // usually already loaded
//   > #load "skia-to-ts.fsx";;
//   > TsGen.generate [ @"../SkiaSharp/bin/Release/net8.0/SkiaSharp.dll" ] @"./skiasharp.all.d.ts" (Some "SkiaSharp");;
// or without root namespace filter:
//   > TsGen.generate [ @"SkiaSharp.dll" ] @"./out.d.ts" None;;
//
// The output faithfully includes:
//   • ALL public types: classes, records, structs, interfaces, delegates, enums (incl. nested types)
//   • ALL public members: fields, properties (incl. indexers), methods (incl. generics), constructors, events, and statics
//   • Classes/structs/records are emitted as TypeScript `interface` (per your rule)
//   • Static members & constructors emitted under companion `export interface <Name>__statics { ... }`
//   • Delegates => `export type Name = (...args) => ret`  |  Enums => `export enum Name { ... }`
//   • Events => `DotNetEvent<DelegateType>`  |  ref/out/in => Ref<T>/Out<T>/In<T>

open System
open System.IO
open System.Text
open System.Reflection
open System.Collections.Generic
open System.Linq

module Ignore =
    let namespaces =
        [
            "SkiaSharp.Internals"
        ]
    let types =
        [
            "SkiaSharp.SkiaSharpVersion"
            "SkiaSharp.SKFrontBufferedStream"
        ]
    let membersToIgnore =
        [
            "GetEnumerator"
            "Dispose"
            "CreateClipIterator"
            "CreateRectIterator"
            "CreateSpanIterator"
            "CreateIterator"
            "CreateRawIterator"
        ]
    let typesInMembers =
        [
            "System.IO.Stream"
            "System.ReadOnlySpan`1"
            "System.Span`1"
        ]


module private Util =
  let isCompilerGenerated (m: MemberInfo) =
    m.GetCustomAttributes(true)
    |> Seq.exists (fun a -> a.GetType().FullName = "System.Runtime.CompilerServices.CompilerGeneratedAttribute")

  let isDeclaredInAssemblies (assemblies: Assembly list) (m: MemberInfo) =
    let declaringType = m.DeclaringType
    assemblies |> List.exists (fun asm -> asm = declaringType.Assembly)

  let hasExcludedType (t: Type) =
    let typeName = if t.IsGenericType then t.GetGenericTypeDefinition().FullName else t.FullName
    Ignore.typesInMembers |> List.contains typeName

  let memberHasExcludedTypes (m: MemberInfo) =
    match m with
    | :? MethodInfo as mi ->
        hasExcludedType mi.ReturnType ||
        mi.GetParameters() |> Array.exists (fun p -> hasExcludedType p.ParameterType)
    | :? PropertyInfo as pi ->
        hasExcludedType pi.PropertyType ||
        pi.GetIndexParameters() |> Array.exists (fun p -> hasExcludedType p.ParameterType)
    | :? FieldInfo as fi ->
        hasExcludedType fi.FieldType
    | :? EventInfo as ei ->
        hasExcludedType ei.EventHandlerType
    | :? ConstructorInfo as ci ->
        ci.GetParameters() |> Array.exists (fun p -> hasExcludedType p.ParameterType)
    | _ -> false

  let sanitize (id: string) =
    let core = id.Replace('`','_').Replace("<","_").Replace(">","_")
    if core.Length > 0 && Char.IsDigit core[0] then "_" + core else core

  let escapeMember (name: string) =
    let keywords = set [ "default";"function";"var";"let";"const";"new";"class";"interface";"enum";"export";"extends";"implements";"package";"private";"protected";"public";"static";"yield" ]
    let needsQuote = keywords.Contains name || name |> Seq.exists (fun ch -> not(Char.IsLetterOrDigit ch || ch = '_' || ch = '$'))
    if needsQuote then $"\"{name}\"" else name

  let isDelegateType (t: Type) =
    let baseT = t.BaseType
    not (obj.ReferenceEquals(baseT,null)) && typeof<MulticastDelegate>.IsAssignableFrom baseT

  let isIndexer (p: PropertyInfo) = p.GetIndexParameters().Length > 0

  let typeParams (t: Type) =
    if t.IsGenericTypeDefinition then
      let args = t.GetGenericArguments() |> Array.map (fun a -> a.Name)
      if args.Length = 0 then "" else "<" + String.Join(", ", args) + ">"
    else ""

  let typeName (t: Type, includeGenerics: bool) =
    let name = t.Name.Split('`')[0] |> sanitize
    if includeGenerics then name + typeParams t else name

  let methodGenerics (mi: MethodInfo) =
    if mi.IsGenericMethodDefinition then
      let args = mi.GetGenericArguments() |> Array.map (fun a -> a.Name)
      if args.Length = 0 then "" else "<" + String.Join(", ", args) + ">"
    else ""

  let underlying (t: Type) = t // Nullable handled in printer

  type Writer() =
    let sb = StringBuilder()
    let mutable indent = 0
    member _.Text = sb.ToString()
    member _.L (line: string) = sb.Append(String(' ', indent*2)).AppendLine(line) |> ignore
    member _.NL() = sb.AppendLine() |> ignore
    member this.Push (header: string) = this.L(header + " {"); indent <- indent + 1
    member this.Pop () = indent <- indent - 1; this.L("}")

  type TypePrinter() =
    let builtins = dict [
      typeof<Void>, "void"; typeof<bool>, "boolean"; typeof<string>, "string"; typeof<char>, "string";
      typeof<byte>, "number"; typeof<sbyte>, "number"; typeof<int16>, "number"; typeof<uint16>, "number";
      typeof<int>, "number"; typeof<uint32>, "number"; typeof<int64>, "number"; typeof<uint64>, "number";
      typeof<single>, "number"; typeof<double>, "number"; typeof<decimal>, "number";
      typeof<obj>, "any"; typeof<IntPtr>, "number"; typeof<UIntPtr>, "number";
      typeof<Guid>, "string"; typeof<DateTime>, "string"; typeof<TimeSpan>, "number"; typeof<Uri>, "string"
    ]

    let programmaticName (name: string) =
      let core = name.Replace("`","_")
      if core.Length > 0 && Char.IsDigit core[0] then "_" + core else core

    let tryImplements (t: Type) (openGeneric: Type) =
      seq { yield! t.GetInterfaces(); yield t }
      |> Seq.tryFind (fun it -> it.IsGenericType && it.GetGenericTypeDefinition() = openGeneric)
      |> Option.map (fun it -> it.GetGenericArguments())

    let rec ts (t: Type) : string =
      let t = if t.IsByRef then t.GetElementType() else t
      if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Nullable<_>> then
        let a = t.GetGenericArguments()[0]
        $"{ts a} | null"
      elif t.IsArray then
        $"{ts (t.GetElementType())}[]"
      elif builtins.ContainsKey t then builtins[t]
      else
        match tryImplements t (typedefof<IDictionary<_,_>>) with
        | Some kv -> $"Record<{ts kv[0]}, {ts kv[1]}>"
        | None ->
          match tryImplements t (typedefof<IEnumerable<_>>) with
          | Some xs -> $"{ts xs[0]}[]"
          | None ->
            if t.IsGenericType then
              let def = t.GetGenericTypeDefinition()
              let name = def.Name.Split('`')[0]
              let args = t.GetGenericArguments() |> Array.map ts |> String.concat ", "
              $"{name}<{args}>"
            else programmaticName t.Name

    member _.TsType(t: Type) = ts t

  let param (p: ParameterInfo) (tp: TypePrinter) =
    let name = p.Name
    let isParams = p.GetCustomAttributes(true) |> Seq.exists (fun a -> a.GetType().FullName = "System.ParamArrayAttribute")
    let mutable t = p.ParameterType
    if isParams then $"...{name}: {tp.TsType t}[]"
    elif p.IsOut then $"{name}: Out<{tp.TsType (t.GetElementType())}>"
    elif t.IsByRef then $"{name}: Ref<{tp.TsType (t.GetElementType())}>"
    elif p.IsIn && t.IsByRef then $"{name}: In<{tp.TsType (t.GetElementType())}>"
    else
      let opt = if p.HasDefaultValue then "?" else ""
      $"{name}{opt}: {tp.TsType t}"

  let methodParams (m: MethodBase) (tp: TypePrinter) =
    m.GetParameters() |> Array.map (fun p -> param p tp) |> String.concat ", "

module TsGen =
  open Util

  let private emitEnum (t: Type) (w: Writer) =
    w.Push ($"export enum {typeName(t, false)}")
    for f in t.GetFields(BindingFlags.Public ||| BindingFlags.Static) do
      if not f.IsSpecialName then
        let v = f.GetRawConstantValue() |> Convert.ToInt64
        w.L ($"{sanitize f.Name} = {v},")
    w.Pop()
    w.NL()

  let private emitDelegate (t: Type) (w: Writer) =
    let invoke = t.GetMethod("Invoke")
    let tp = TypePrinter()
    let pars = methodParams invoke tp
    let ret = tp.TsType invoke.ReturnType
    w.L ($"export type {typeName(t,true)} = ({pars}) => {ret};")
    w.NL()

  let private emitInterfaceLike (t: Type) (w: Writer) (assemblies: Assembly list) =
    let tp = TypePrinter()
    // Instance surface
    w.Push ($"export interface {typeName(t,false)}{typeParams t}")

    for f in t.GetFields(BindingFlags.Public ||| BindingFlags.Instance) do
      if not f.IsSpecialName && not (isCompilerGenerated f) && isDeclaredInAssemblies assemblies f && not (memberHasExcludedTypes f) && not (Ignore.membersToIgnore |> List.contains f.Name) then
        w.L ($"{escapeMember f.Name}: {tp.TsType f.FieldType};")

    for p in t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance) do
      if isDeclaredInAssemblies assemblies p && not (memberHasExcludedTypes p) && not (Ignore.membersToIgnore |> List.contains p.Name) then
        if isIndexer p then
          let pars = p.GetIndexParameters() |> Array.map (fun ip -> param ip tp) |> String.concat ", "
          w.L ($"get_Item({pars}): {tp.TsType (underlying p.PropertyType)};")
        else
          let ro = if p.CanWrite then "" else "readonly "
          w.L ($"{ro}{escapeMember p.Name}: {tp.TsType (underlying p.PropertyType)};")

    for e in t.GetEvents(BindingFlags.Public ||| BindingFlags.Instance) do
      if isDeclaredInAssemblies assemblies e && not (memberHasExcludedTypes e) && not (Ignore.membersToIgnore |> List.contains e.Name) then
        w.L ($"{escapeMember e.Name}: DotNetEvent<{TypePrinter().TsType e.EventHandlerType}>;")

    t.GetMethods(BindingFlags.Public ||| BindingFlags.Instance)
    |> Array.filter (fun m -> not m.IsSpecialName && isDeclaredInAssemblies assemblies m && not (memberHasExcludedTypes m) && not (Ignore.membersToIgnore |> List.contains m.Name))
    |> Array.sortBy (fun m -> m.Name, m.GetParameters().Length)
    |> Array.iter (fun m ->
         w.L ($"{escapeMember m.Name}{methodGenerics m}({methodParams m tp}): {tp.TsType m.ReturnType};")
       )

    w.Pop()

    // Statics & constructors
    let statics = t.GetFields(BindingFlags.Public ||| BindingFlags.Static) |> Array.filter (fun f -> not f.IsSpecialName && isDeclaredInAssemblies assemblies f && not (memberHasExcludedTypes f) && not (Ignore.membersToIgnore |> List.contains f.Name))
    let sprops = t.GetProperties(BindingFlags.Public ||| BindingFlags.Static) |> Array.filter (fun p -> isDeclaredInAssemblies assemblies p && not (memberHasExcludedTypes p) && not (Ignore.membersToIgnore |> List.contains p.Name))
    let smethods = t.GetMethods(BindingFlags.Public ||| BindingFlags.Static) |> Array.filter (fun m -> not m.IsSpecialName && isDeclaredInAssemblies assemblies m && not (memberHasExcludedTypes m) && not (Ignore.membersToIgnore |> List.contains m.Name))
    let ctors = t.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance) |> Array.filter (fun c -> not (memberHasExcludedTypes c))

    if statics.Length>0 || sprops.Length>0 || smethods.Length>0 || ctors.Length>0 then
      w.Push ($"export interface {typeName(t,false)}__statics")
      for c in ctors do
        w.L ($"new({methodParams c tp}): {typeName(t,false)}{typeParams t};")
      for f in statics do
        w.L ($"{escapeMember f.Name}: {tp.TsType f.FieldType};")
      for p in sprops do
        w.L ($"{escapeMember p.Name}: {tp.TsType (underlying p.PropertyType)};")
      for m in smethods do
        w.L ($"{escapeMember m.Name}{methodGenerics m}({methodParams m tp}): {tp.TsType m.ReturnType};")
      w.Pop(); w.NL()

  let rec private emitTypeRecursive (t: Type) (w: Writer) (assemblies: Assembly list) =
    if not (isCompilerGenerated t) then
      if t.IsEnum then emitEnum t w
      elif isDelegateType t then emitDelegate t w
      elif t.IsInterface || t.IsClass || (t.IsValueType && not t.IsEnum) then emitInterfaceLike t w assemblies
      else ()
      let nested = t.GetNestedTypes(BindingFlags.Public)
      if nested.Length > 0 then
        w.Push ($"export namespace {sanitize t.Name}")
        nested |> Array.sortBy (fun nt -> nt.Name) |> Array.iter (fun nt -> emitTypeRecursive nt w assemblies)
        w.Pop()

  let private openNamespaces (parts: string[]) (w: Writer) =
    parts |> Array.iter (fun p -> w.Push ($"export namespace {sanitize p}"))
  let private closeNamespaces (count: int) (w: Writer) =
    for _ in 1..count do w.Pop()
    w.NL()

  let private header (w: Writer) (assemblies: Assembly list) =
    w.L "// AUTO-GENERATED by skia-to-ts.fsx — DO NOT EDIT"
    w.L "// Source assemblies:"
    assemblies |> List.iter (fun asm -> w.L ($"//   - {asm.FullName}"))
    w.NL()
    w.L "/* Utility types for ref/out/in and events */"
    w.L "export type Ref<T> = { value: T };"
    w.L "export type Out<T> = { value?: T };"
    w.L "export type In<T> = { readonly value: T };"
    w.L "export interface DotNetEvent<THandler extends Function> { add(handler: THandler): void; remove(handler: THandler): void; }"
    w.NL()

  let generate (assemblyPaths: string list) (outputPath: string) (rootNamespace: string option) =
    let loadAsm p =
      if not (File.Exists p) then failwithf "Assembly not found: %s" p
      Assembly.LoadFrom (Path.GetFullPath p)
    let assemblies = assemblyPaths |> List.map loadAsm

    // Collect exported types from all assemblies
    let exported =
      assemblies
      |> Seq.collect (fun asm ->
           try asm.GetExportedTypes() :> seq<_>
           with :? ReflectionTypeLoadException as ex -> ex.Types)
      |> Seq.distinct
      |> Seq.filter (fun t ->
            Ignore.namespaces |> List.contains t.Namespace |> not
            && Ignore.types |> List.contains t.FullName |> not)
      |> Seq.sortBy (fun t ->
        t.Namespace,
        (
            if obj.ReferenceEquals(t.DeclaringType, null)
            then t.FullName
            else t.DeclaringType.FullName + "." + t.Name),
        t.Name)
      |> Seq.toArray

    let w = Writer()
    header w assemblies

    exported
    |> Seq.filter (fun t -> not (isCompilerGenerated t))
    |> Seq.groupBy (fun t -> t.Namespace)
    |> Seq.sortBy fst
    |> Seq.iter (fun (ns, types) ->
        // namespace filtering
        match rootNamespace with
        | Some root when not (ns = root || ns.StartsWith(root + ".", StringComparison.Ordinal)) -> ()
        | _ ->
          let parts = if String.IsNullOrEmpty ns then [||] else ns.Split('.')
          openNamespaces parts w
          types
          |> Seq.filter (fun t -> obj.ReferenceEquals(t.DeclaringType,null))
          |> Seq.iter (fun t -> emitTypeRecursive t w assemblies)
          closeNamespaces parts.Length w
      )

    let outPath = Path.GetFullPath outputPath
    File.WriteAllText(outPath, w.Text, Encoding.UTF8)
    printfn "Wrote %s" outPath

    ()
