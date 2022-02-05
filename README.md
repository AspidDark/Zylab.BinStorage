# Zylab.BinStorage

<h2>Used 3rd party libraries:</h2>

NewtonsiftJson

<h2>Descrition:</h2>

<p>All file size limits applied from setup.</p>
<p>As a cacheing was chosen Limited by memory Hashtable(object inserted\deleted according to read count).Cacheing file could be settuped.</p>
<p>Used minimal(as I think) locking schema.</p>
<p>Saveing to index file uses Bson serializer, not best approch but rwiting own good working srilizer would be long sory.</p>

<h2>Limitation:</h2>

Memory writing /reading buffers limitation as const ~80Kb less than LOH

