namespace Sproc.Lock.OO

open Sproc.Lock.Fun

/// Exception thrown by ``LockProvider`` if none of the specified locks are available.
type LockUnavailableException (message) =
    inherit System.Exception(message)

/// Exception thrown by ``LockProvider`` if a lock request errors on SQL Server.
/// ``LockErrorCode`` is the SQL error response.
type LockRequestErrorException (errorCode) as this =
    inherit System.Exception(sprintf "Error code: %d" errorCode)
    do
        this.Data.Add(box "ErrorCode", box errorCode)
    member x.LockErrorCode 
        with get () =
            x.Data.["ErrorCode"] |> unbox<int>

/// Class representing a single lock server. ``connString`` is a complete SQL Server connection string, including credentials.
type LockProvider (connString : string) =
    let OOise lockId getLock =
        match getLock lockId with
        | Locked l -> l
        | Unavailable -> raise <| LockUnavailableException(sprintf "Lock %s was unavailable." lockId)
        | Error i -> raise <| LockRequestErrorException i
        
    /// Attempts to acquire a global lock from the provider with
    /// the specified lockIdentifier. After maxDuration has elapsed the
    /// lock will become "stale" and will be automatically released to the
    /// next requester.
    member x.GlobalLock (lockId, maxDuration) =
        GetGlobalLock connString maxDuration |> OOise lockId
    /// Attempts to acquire an organisation lock from the provider with
    /// the specified lockIdentifier. After maxDuration has elapsed the
    /// lock will become "stale" and will be automatically released to the
    /// next requester.
    member x.OrganisationLock (lockId, organisation, maxDuration) =
        GetOrganisationLock connString organisation maxDuration |> OOise lockId
    /// Attempts to acquire an environment lock from the provider with
    /// the specified lockIdentifier. After maxDuration has elapsed the
    /// lock will become "stale" and will be automatically released to the
    /// next requester.
    member x.EnvironmentLock (lockId, organisation, environment, maxDuration) =
        GetEnvironmentLock connString organisation environment maxDuration |> OOise lockId
    /// As ``GlobalLock``, but waiting until ``timeOut`` or the lock is available
    member x.AwaitGlobalLock (lockId, maxDuration, timeOut, pollInterval) =
        (fun lockId -> AwaitLock timeOut (fun () -> GetGlobalLock connString maxDuration lockId)) |> OOise lockId
    /// As ``OrganisationLock``, but waiting until ``timeOut`` or the lock is available
    member x.AwaitOrganisationLock (lockId, organisation, maxDuration, timeOut, pollInterval) =
        (fun lockId -> AwaitLock timeOut (fun () -> GetOrganisationLock connString organisation maxDuration lockId)) |> OOise lockId
    /// As ``EnvironmentLock``, but waiting until ``timeOut`` or the lock is available
    member x.AwaitEnvironmentLock (lockId, organisation, environment, maxDuration, timeOut, pollInterval) =
        (fun lockId -> AwaitLock timeOut (fun () -> GetEnvironmentLock connString organisation environment maxDuration lockId)) |> OOise lockId
    /// Build a ``System.Func`` that returns a lock based on lockId and provide a list of lockIds.
    /// If any of the locks are available, it will pick one of the available locks at random.
    member x.OneOf<'t> (getLock : System.Func<'t, Lock>, lockIds) =
        let getLock' =
            fun t ->
                try
                    getLock.Invoke t |> Locked
                with
                | :? LockUnavailableException -> Unavailable
                | :? LockRequestErrorException as e -> Error (e.Data.["ErrorCode"] :?> int)
        match OneOfLocks getLock' lockIds with
        | Locked l -> l
        | Unavailable -> raise <| LockUnavailableException(sprintf "None of the locks %A were available." lockIds)
        | Error i -> raise <| LockRequestErrorException i
    /// Build a ``System.Func`` that returns a lock based on lockId and provide a list of lockIds.
    /// If any of the locks are available, it will pick one of the available locks at random.
    /// If none are available it will wait until one is, or ``timeOut`` has passed.
    member x.AwaitOneOf<'t> (getLock : System.Func<'t, Lock>, lockIds, timeOut, pollInterval) =
        let getLock' =
            fun t ->
                try
                    getLock.Invoke t |> Locked
                with
                | :? LockUnavailableException -> Unavailable
                | :? LockRequestErrorException as e -> Error (e.Data.["ErrorCode"] :?> int)
        match AwaitLock timeOut (fun () -> OneOfLocks getLock' lockIds) with
        | Locked l -> l
        | Unavailable -> raise <| LockUnavailableException(sprintf "None of the locks %A were available." lockIds)
        | Error i -> raise <| LockRequestErrorException i
