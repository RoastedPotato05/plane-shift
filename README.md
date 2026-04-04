# plane-shift

controls: tab to switch cameras, in the 3D box view hold right click to pan the camera, click and drag to move objects that are able to be moved, in the 2D ship view use A and D to turn, W to accelerate forwards

recommend using the Level 1 scene as a template probably

general rundown:
- all you really need to worry about is in the Prefabs folder, theres a couple custom assets i made and a bunch of folders with assets i found on the asset store
- make a copy of Level 1, in the scene theres a couple layer things stacked on top of another and a huge sphere of space background
- inside the sphere is where the box of objects is, you dont really need to mess with big stack of layers unless you want to move around the 2DBorder objects or the green goal object
- if you do you can copy whatever or if you make more walls just put them around the same like y-level layer as the other ones, make sure to give it a Wall tag that is very important
- (Wall tag will designate what the player bounces off of, Obstacle tag is what kills the player, Goal tag is what the goal is blah blah blah)
- as for objects in the box it's set up like this:
- for any object you want to work properly as like an obstacle, first make a solid color material and give it the DoubleSidedTexture shader (this makes the cross sections show up in the other area) and apply it to the object
- if it's an object you want the player to be able to move, give it one of the Movable tags depending on what direction you want them to be able to move it in (i would not recommend XYZ since it's kinda weird to control)
- if you dont want the player to be able to move it just leave it untagged, or theres also a SlicerIgnore tag that makes the slicer ignore the object
- give it the MovementBounds script, this will let you choose how far in any direction it will be able to move (if you want it to move up and down, go to the script in the object's inspector and check the Constrain Y box, you will see an orange volume that shows how far it can move, then you can change the min and max values to whatever)
- btw i mentioned the Obstacle tag earlier you dont have to manually add that or anything the script does it automatically
- thankfully (b/c me and also vscode ai is a genius) the cel shading and arrows and stuff that show up on objects applies automatically all you have to do is give it the tag you want
- once you make the level just save the scene, i have a folder in prefabs where you can put a prefab of the box and the objects just in case but its not really necessary or anything
- i think that's pretty much what you need to know for level design you can just do whatever try to think of some good puzzles you can designate anything as movable btw even the slicer object so you could do something fun with that probably
