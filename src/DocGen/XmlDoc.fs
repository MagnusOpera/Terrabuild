module XmlDoc
open System.IO
open System.Xml.Serialization

[<CLIMutable>]
type Assembly = {
    [<XmlElement("name")>]
    Name: string
}

[<CLIMutable>]
type Param = {
    [<XmlAttribute("name")>]
    Name: string

    [<XmlAttribute("required")>]
    Required: bool

    [<XmlAttribute("demo")>]
    Demo: string

    [<XmlText>]
    Body: string
}

[<CLIMutable>]
type Summary = {
    [<XmlAttribute("weight")>]
    Weight: int

    [<XmlAttribute("title")>]
    Title: string

    [<XmlText>]
    Body: string
}


[<CLIMutable>]
type Member = {
    [<XmlAttribute("name")>]
    Name: string

    [<XmlElement("summary")>]
    Summary: Summary

    [<XmlElement(ElementName = "param")>]
    Params: Param[]
}

[<CLIMutable; XmlRoot("doc")>]
type Doc = {
    [<XmlElement("assembly")>]
    Assembly: Assembly

    [<XmlArray("members"); XmlArrayItem("member")>]
    Members: Member[]
}

let load (filename: string) =
    let xmlSerializer = XmlSerializer(typeof<Doc>)
    use stream = File.OpenRead(filename)
    let result = xmlSerializer.Deserialize(stream) :?> Doc
    result
