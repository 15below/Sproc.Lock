### 2.0.0 - Update FSharp.Core, FsCheck
* Updated FSharp.Core to 4.2.2 (4.4.1.0)
* Updated FsCheck to 2.9

### 1.0.4 - Fix NuGet package (2)
* Include FSharp.Core dependency
* Correct package location to net45

### 1.0.3 - Fix NuGet package
* Include FSharp.Core dependency
* Correct package location to net45

### 1.0.2 - Update FSharp.Core, FsCheck, and .Net version 
* Updated FSharp.Core to 4.0.0.1 (4.4.0.0)
* Updated FsCheck to 2.0.6
* Updated Sproc.Lock to target .Net 4.5

### 1.0.1 - Octopus Deploy
* Add build step to create Octopus Deploy package

### 1.0.0 - Release
* Released


### 1.0.0-beta6 - Volume test tweaks
* Tweak polling based on load testing

### 1.0.0-beta5 - Packaging Fix
* Add FSharp.Core as nuget dependency (Fixes issue #1)
* Minor documentation fixes

### 1.0.0-beta4 - Bug fix
* removed unused pollInterval parameters from OO interface
* Fix TimeOut exception for awaited locks (new returns Unavailable, as designed)
* Update css/template

### 1.0.0-beta3 - Adding description
* Hashed values are indexed in lock tables, but a human readable description is also added for debugging.
* Database re-baselined

### 1.0.0-beta2 - Updated docs
* Proof read, css fixes

### 1.0.0-beta1 - Adaptive polling
* Remove polling interval parameter from Await methods
* Make Await polling "adaptive" (rapid initially, throttling back if lock unavailable)

### 1.0.0-alpha1 - Ready for testing version
* Expanded FsCheck properties for generated organisation/environment names
* Pre-hashing of sproc arguments to allow arbitrary length arguments
* Sproc performance improvements
* Tables baselined with 44 character columns
* Restyled documentation

### 0.9.2 - Unreleased
* Added additional FsCheck property
* Saner ordering of IF statements in sprocs

### 0.9.1 - Unreleased
* Minor documentation fix

### 0.9.0 - Unreleased
* F#/C# joint tutorial first cut
* CI builds reliable
