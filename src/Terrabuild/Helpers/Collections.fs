module Collections

type map<'K, 'V when 'K : comparison> = Map<'K, 'V>

type set<'T when 'T : comparison> = Set<'T>

let emptyIfNull<'t when 't: null and 't : (new : unit -> 't)> (c: 't) =
    if c |> isNull then new 't()
    else c

let (?) (q: bool) (yes: 'a, no: 'a) = if q then yes else no

module Map =
    let ofDict dic = 
        dic 
        |> Seq.map (|KeyValue|)  
        |> Map

    let choose f m =
        m |> Map.fold (fun acc k v -> match f k v with
                                      | Some x -> acc |> Map.add k x
                                      | _ -> acc) Map.empty

    let replace addMap sourceMap =
        addMap |> Map.fold (fun acc key value -> Map.add key value acc) sourceMap
