This project was developed for week 4 of my 52 pojects a week challenge, and it embodies the complete idea behind the challenge. 

Before this project my knowledge of neural networks went as far as knowing they had multiple layers with weightings between nodes, along with some sort of reward and punishment system. Developing this project has taught me about backpropagation, epochs, training and validation loss, scalers, and the ideas associated with pytorch and its paradigms.

This was a massive project for me to complete in a few days, and without the use of AI I would not have completed it. This is not to say I sat back, prompting and letting it figure it out itself. I educated myself before the project began, with videos from [3 Blue 1 Brown](https://youtu.be/aircAruvnKk?si=VGzBxu4eJKmYWbsP) and [Rob Mulla](https://youtu.be/tHL5STNJKag?si=4hu3c2ski43e31El). The use of AI allowed me to generate the Unity code faster than I'd be able to type it, and thanks to my previous experience with Unity, I was able to quickly fix any hallucinations Claude would have. The nature of pytorch and its paradigms allowed me to easily understand the generated neural network code, along with enabling me to easily make tweaks that were needed.

The development of the project was steady, and when I got to the point of training the nerual network for the first time, along with verifying its accuracy through tests, I was ecstatic with the progress I had made, but with most projects, this feeling of decent progress didn't last long. I then battled trying to convert the neural network to Onnx, and then with the implementation of the model in Unity. Once I was finally able to call on the predictions, I realised I had an issue, which still persists. 

Although the accuracy of the model on paper, was in the high 90s, in reality it wasn't that great. It really struggled with any sort of top spin deliveries hitting the upper half of the pad, even if clearly missing. Upon realising this, I attempted throughout a day, to find a good balance between the following areas: time taken to record the data, random deliveries, and 'tricky decision' deliveries. In the end there were improvements made to some parts of the model, and unfortunately regressions made to other parts. But as I said earlier, this project embodies the challenge. A short project phase allowing me to learn new technologies rapidly, without getting bogged down by details. It taught me a lot, even thought it is a very rough, unpolished project. 

So what would I do if I could do it again? 

I'd remove the 'time since delivery' feature. This feature, along with how the unity components were set-up, made it tricky for me to decide when and how to call the decision system. Without it I would be able to call the decision upon pad hits, a more realistic scenario. 

Next I would only record data points where the pad is hit. Real version of this technology use multiple cameras, taking multiple points of data in a second, but this is to determine stats like impact height, impact line, ball velocity, ball spin, and distance to the stumps. As I would be taking data from the system, there would be no need for these multiple snapshots. I could simply take the place of impact (X,Y), velocity (X,Y), Spin(Direcetion and Magnitude), and whether or not that ball would have gone on to hit the stumps.

I would also greatly increase the number of deliveries recorded, from 25000 frames of data from around 1500 deliveries, I'd prefer to have around 10000 deliveries recorded. Even with the lower number of frames of data points to work with, I believe the increase in deliveries worked with would lead to a larger, real improvement.

I would have also liked to experiment with different levels of spin, rather than a standrad of 10 Units between the two types. 

Although the end result isn't as polished or accurate as I would have liked, this project has been my favourite so far, attempting to recreate real technology I see all the time. It was definitely worth it in the end, and has given me a lot of excitement towards working with AI in the future.
