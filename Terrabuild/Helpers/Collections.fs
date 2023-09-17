module Helpers.Collections

// [<AutoOpen>]
// module Set =
//     type set<'T when 'T : comparison> = Set<'T>

// [<AutoOpen>]
// module Map =
//     // type map<'K, 'V when 'K : comparison> = Map<'K, 'V>

//     let choose f m =
//         m |> Map.fold (fun acc k v -> match f k v with
//                                       | Some x -> acc |> Map.add k x
//                                       | _ -> acc) Map.empty

// let (?) (q: bool) (yes: 'a, no: 'a) = if q then yes else no

let emptyIfNull<'t when 't: null and 't : (new : unit -> 't)> (c: 't) =
    if c |> isNull then new 't()
    else c

module Map =
    let ofDict dic = 
        dic 
        |> Seq.map (|KeyValue|)  
        |> Map.ofSeq
