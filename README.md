# Zylab.BinStorage

<h2>Used 3rd party libraries:</h2>

NewtonsiftJson

Descrition:

All file size limits applied from setup.
As a cacheing was chosen Limited by memory Hashtable(object inserted\deleted according to read count).Cacheing file could be settuped
Used minimal(as I think) locking schema
Saveing to index file uses Bson serializer, not best approch but rwiting own good working srilizer would be long sory

Limitation:

Memory writing /reading buffers limitation as const ~80Kb less than LOH

