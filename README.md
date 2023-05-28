# AnimationScripts
A few scripts regarding unity animations I've found useful for myself.

## Parameter Renamer
Renames a parameter across the entire avatar. Includes: Transitions, Menus, Expression Parameters, Controller Parameters, Blendtree parameters.

## AnimationRepather
Shows all objects modified by animations in a controller to allow for repathing. Probably a worse version of something Dreadrith made IDK.

## AnimationReplacer
Shows all animations in a controller, allows you to replace all instances of one animation with another one.

## AnimationTemplater
This one is a tad more complicated. You put in a template file and it replaces animations in the controller based on the template:

sample template file:
```json
{
  "Systems":{
    "keyData":[
      "TestSystem1"
    ],
    "valueData":[
      {
        "keyData": [
          "noEdit",
          "humanoid"
        ],
        "valueData": [
          {
            "keyData": [
              "Animation 1",
              "Animation 2"
            ],
            "valueData": [
              "TemplateAnim1",
              "TemplateAnim2"
            ]
          },
          {
            "keyData": [
              "Animation 1"
            ],
            "valueData": [
              "TemplateAnim3"
            ]
          }
        ]
      }
    ]
  }
}
```

This template will make it so that (only in layers with "TestSystem1" in the layer name), all instances of an animation named "TemplateAnim1" will  get replaced by whatever the user enters into the Animation 1 slot. Same with TemplateAnim2 and Animation 2. It will also replace TemplateAnim3 by only the humanoid parts of the animation in the Animation 1 slot.

This system might be useful if you're making an advanced controller prefab where you want the user to enter their own animations but want to process them in certain ways.