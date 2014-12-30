(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

(**
Sproc.Lock
==========

Documentation

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The Sproc.Lock library can be <a href="https://nuget.org/packages/Sproc.Lock">installed from NuGet</a>:
      <pre>PM> Install-Package Sproc.Lock</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Example
-------

Take a distributed lock in a shared SQL Server database.

In F#:

*)
#r "Sproc.Lock.dll"
open System
open Sproc.Lock.Fun

let connString = "sql server connection string"

let lock1 = GetGlobalLock connString (TimeSpan.FromMinutes 5.) "MyAppLock"

match lock1 with
| Locked l ->
    sprintf "I got a lock called %s!" l.LockId
    // (will be "MyAppLock" on this occassion)
| Unavailable ->
    sprintf "Someone else had the lock already!"
| Error i ->
    sprintf "Something went wrong - error code: %d" i

lock1.Dispose()

(**

And C#:

    [lang=csharp]
    using System;
    using Sproc.Lock.OO;


    namespace MyApp
    {
        class Thing
        {
            static void DoLockRequiringWork()
            {
                var provider = new LockProvider("sql connection string");
                try
                {
                    using (var lock2 = provider.GlobalLock("MyAppLock", TimeSpan.FromMinutes(5.0))
                    {
                        // If I get here, I've got a lock!                    
                        // Doing stuff!
                    } // Lock released when Disposed
                }
                catch (LockUnavailableException)
                {
                    // Couldn't get the lock                
                    throw;
                }
                catch (LockRequestErrorException)
                {
                    // Getting the lock threw an error
                    throw;
                }
            }
        }
    }

Documentation
-------------

Sproc.Lock is a combination of a managed .net library and a set of SQL Server scripts that
combine to turn SQL Server into a distributed locking server.

This library is only really recommended if you are already using SQL Server, and do not
have a more suitable distibuted locking server already up and running. In that case, Sproc.Lock
can save you the overhead of adding an additional piece of infrastructure to your environment.

Basic Concepts
--------------

Sproc.Lock makes use of a few concepts that might not be immediately obvious.

Firstly, all methods that acquire a lock must specify a maximum duration it will be held for.
This is essential in distributed locking systems, as otherwise a crashing/abnormally behaving
service may acquire a lock and never release it.

Secondly, Sproc.Lock has three levels of scoping a lock. Locks can be acquired as:

* Global - no other global lock with the same lock id can be acquired at the same time
* Organisation - an organisation id is also specified, and locks with the same id can be held by multiple organisations
* Environment - an organisation id and environment type are specified; locks are scoped within both

This is designed to cope with the common situations of having resources that are available in limited number:

* across a data centre
* across an client organisation
* within a particular environment (i.e. Production, Test, etc)

Find Out More!
--------------

 * [Tutorial](tutorial.html) contains a further explanation of Sproc.Lock.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.
 
Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding a new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under Public Domain license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/fsprojects/Sproc.Lock/tree/master/docs/content
  [gh]: https://github.com/fsprojects/Sproc.Lock
  [issues]: https://github.com/fsprojects/Sproc.Lock/issues
  [readme]: https://github.com/fsprojects/Sproc.Lock/blob/master/README.md
  [license]: https://github.com/fsprojects/Sproc.Lock/blob/master/LICENSE.txt
*)
