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
