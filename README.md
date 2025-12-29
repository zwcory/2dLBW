This project was developed in Unity, with pytorch used to develop the neural network. The model was then exported to ONNX, for unity compatibility.

Read [my thoughts](Thoughts) of the development of the project.

The builds will be released to the releases page, as I have done with previous unity projects before. 

I have left some of the older models and their scalers in the project, so you can test them out in the project, unity hub is required, but simply replace the Onnx model and the scalers, found in Assets/Resources. Visualisations of the model can be found in Python/Images.

FastRecord records data for training, although it could also be used for testing. The buttons (Fast record and test record) just export the results under a different name, and with a different number of entries. 

In the end I found a balance between random , more focused, and very focused deliveries delivered the best result in game. I would make changes to the model, and how its called if I were to recreate the project, see [my thoughts](Thoughts) for the details.

This is not meant to be a fully polished project, but rather a chance to develop and learn the ideas around AI, neural networks, and pytorch, all within a short project phase. 
