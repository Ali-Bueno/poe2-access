using System.Runtime.InteropServices; using System.Text;
using LibBundle3; using LibBundle3.Records;
using Index = LibBundle3.Index;
NativeLibrary.SetDllImportResolver(typeof(Index).Assembly, (n,a,s)=> Path.GetFileNameWithoutExtension(n).Equals("oo2core",StringComparison.OrdinalIgnoreCase)? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory,"oo2core.dll")):IntPtr.Zero);
// args: gameDir outDir prefix... ; extensions via env EXTS
var game=args[0]; var outDir=args[1]; var pre=args.Skip(2).ToArray();
var exts=(Environment.GetEnvironmentVariable("EXTS")??".ot,.act").Split(',');
var dir=Path.Combine(game,"Bundles2");
using var idxf=new FileStream(Path.Combine(dir,"_.index.bin"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
var copy=new MemoryStream(); idxf.CopyTo(copy); copy.Position=0;
using var idx=new Index(copy,false,false,new Fac(dir));
idx.ParsePaths();
int n=0;
foreach(var f in idx.Files.Values){ var p=f.Path; if(p==null) continue; var l=p.ToLowerInvariant();
 if(!exts.Any(e=>l.EndsWith(e))||!pre.Any(x=>l.StartsWith(x))) continue;
 var o=Path.Combine(outDir,l.Replace('/','_')); if(Environment.GetEnvironmentVariable("BIN")=="1") File.WriteAllBytes(o,f.Read().ToArray()); else File.WriteAllText(o,Encoding.Unicode.GetString(f.Read().ToArray()),Encoding.UTF8); n++; }
Console.WriteLine(n);
class Fac:IBundleFactory{ string d; public Fac(string d){this.d=d;}
 public Bundle GetBundle(BundleRecord r){ var s=new FileStream(Path.Join(d,r.Path),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete); return new Bundle(s,false,r);}
 public Stream CreateBundle(string p)=>throw new NotSupportedException();
 public bool DeleteBundle(string p)=>throw new NotSupportedException(); }
