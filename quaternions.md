
# About Rotations

Rotations in unity are defined as quaternions. But what does the rotation, a quaternion, of an object actually mean?

A quaternion defines how to get from one rotation to another.
The world rotation of an object can be thought about as being the rotation operation needed to get from `Quaternion.identity` ("no rotation") to the object's world rotation.
The local rotation is the rotation operation needed to get from the parent object's world rotation to the object's own world rotation.

In code:
```cs
Transform myTransform;
Quaternion worldRotation;

// World rotation:

worldRotation = Quaternion.identity * myTransform.rotation;
// Equivalent to
worldRotation = myTransform.rotation;
// since 'Quaternion.identity *' has no effect on the rotation.

// Local rotation:

worldRotation = myTransform.parent.rotation * myTransform.localRotation;
// But what if myTransform is a root object which has no parent?
// Well a lack of a parent effectively means that the "parent" rotation is Quaternion.identity.
Quaternion parentRotation;
if (myTransform.parent == null)
    parentRotation = Quaternion.identity;
else
    parentRotation = myTransform.parent.rotation;
worldRotation = parentRotation * myTransform.localRotation;
// That if condition can be shortened, but this is clear for everybody to understand.

// It follows that for objects without parents, their local and world rotations are equivalent.
```

## Combining and Order

Since quaternions define how to get from one rotation to another, it is natural for them to be able to be combined. In unity this is done using the `*` operator, and the code snippet above is already doing just that.

Do note that I have heard that using the `*` operator for this type of operation with quaternions is unconventional in math, however that is how it is in unity.

What unity's `*` operator for quaternion means is "first rotate by the left hand side (the left operand), then rotate by the right hand side (the right operand)".

The order is significant. `a * b` is not equivalent to `b * a`.

A good intuition I have found to understand why this is the case is a simple demonstration one can do using one of their hands (preferably the left hand for this example):
- Make finger guns with a hand
- The index finger is the forward direction
- The thumb is the up direction
- Now we define 2 rotations, rotating to the "right" and "up".
  - Rotating "right" means rotating the hand around the thumb, swinging the index finger to the right 90 degrees
  - Rotating "up" means tilting the hand back 90 degrees, to be clear:
    - Imagine a plane, both the index finger and thumb lay flat on that plane, so it's kind of like the palm as a whole lays on that plane
    - Rotate the hand up/back such that the index finger ends up in the direction that the thumb was in
- With these "right" and "up" rotations defined, try the following:
  - Rotate "right" then "up"
  - Observe in what rotation your hand ended up in
  - Return to your original hand rotation
  - Rotate "up" then "right"
    - Ensure to remember how "right" means rotating "around the thumb", the thumb's direction does not change
  - Observe how your hand is in a different rotation than "right" then "up"

When rotating "right" then "up", the index finger ends up pointing up, and the thumb to the left.
When rotation "up" then "right", the index finger ends up pointing to the right, and the thumb to the back.

These are 2 entirely different rotations the hand ended up in. I encourage you to go through those rotations a few times to gather an understanding of how that comes to be.

There's multiple ways to describe it. Some ways I can think of:
- the first rotation changes the frame of reference for the second rotation
- the second rotation is in the space defined by the first rotation
- the second rotation is relative to the first
- the second rotation is local to the first rotation

"Local to" the first rotation... we've seen this before. `parentRotation * myTransform.localRotation`. That `localRotation` rotation is local to the `parentRotation`, it is in the parent's local space, and is therefore the second operand for the `*` operator, the right hand side, the second rotation. This now also shows how and why `myTransform.localRotation * parentRotation` would be nonsensical.

## Rotation Offsets

When working with quaternions and combining them, there's very few operations available to us, at least in unity and to my knowledge. It is effectively just `*` to combine them. But there is also `Quaternion.Inverse()`. For some desired rotation math it actually requires to work with direction vectors instead, and then converting those back to quaternions. For right now we'll stick with purely quaternion operations.

Say we have 2 quaternions. They're world space rotations of 2 different objects. Our goal is to figure out what rotation we must perform in order to get from one object's rotation to the other. Similar to how one might want to calculate the position offsets of 2 objects. The intent being to use that rotational offset later on to calculate what the second object's rotation should be after the first object's rotation has changed.

One could loosely compare combining quaternions using `*` as "adding" them together. But what if one wanted to "subtract" a rotation from another? That is how we would calculate positional offsets of 2 objects after all, subtracting one's position from the other. We won't however use the specifics of vector math as a way to figure out what quaternion math should be, and you will see why later on.

This is where `Quaternion.Inverse()` comes in, however it is not identical to subtraction. The inverse quaternion is effectively rotating in the opposite direction of a given quaternion.

Using the same practical setup as before with the finger guns and rotating to the "right" 90 degrees, as in swinging the index finger around the thumb, the inverse of this "right" rotation is doing the exact same thing, but singing to the left.

You can mime out the following operation `rotateRight * Quaternion.Inverse(rotateRight)`, swinging the index finger to the right, and then to the left, and to no surprise the hand ends up in the exact same rotation as it started. In other words `rotateRight * Quaternion.Inverse(rotateRight) == Quaternion.identity`.

Now knowing that `Quaternion.Inverse(rotateRight)` in our example means swinging to the left 90 degrees, well, naturally by performing just that rotation without swinging to the right first results in the hand's index finger pointing to the left. It might help to think about the inverse quaternion being a mirror of the input quaternion.

Back to our goal, given 2 world rotations `a` and `b`, calculate a rotation which defines rotating from one rotation to the other. So `a * rotationFromAToB == b`, and `rotationFromAToB` is currently unknown.

Let's do something very silly:
- `Quaternion.identity * b == b`, right, `Quaternion.identity` is the "don't rotate" rotation after all
- Recall `rotateRight * Quaternion.Inverse(rotateRight) == Quaternion.identity`... now replace `rotateRight` with `a`, since our prior example doesn't just apply to doing hand gestures
- We get `a * Quaternion.Inverse(a) == Quaternion.identity`
- Now since `a * Quaternion.Inverse(a)` is `Quaternion.identity`... what if we take that and put it in `Quaternion.identity * b == b`?
- So `a * Quaternion.Inverse(a) * b == b`
- Compare that with `a * rotationFromAToB == b`, which is our goal
- Curious, that first `a` and the final `b` match, it is almost as though `Quaternion.Inverse(a) * b == rotationFromAToB`

And not just almost, that is the case! But _why_? It looks as though I just did random substitutions and we get an obscure `Quaternion.Inverse(a) * b` without any clear intuition.

If we think about positions for a moment, `posA + offsetFromAToB == posB`, we know that we can subtract `posA` from both sides of that equation and we get `offsetFromAToB == posB - posA`.

I did not however give that example previously, because looking at `posB - posA` one might come to the conclusion that one could do the same for rotations, that being `b * Quaternion.Inverse(a)`. Comparing that with `Quaternion.Inverse(a) * b`... it's the same operands, however their order is flipped. And remember from before how the order is significant. This shows why I would discourage using vector math as a comparison, because it doesn't give us any intuition about the order of operations.

Back to those equations I threw together while being silly, there is a specific one in those steps we can gain intuition from, that being `a * Quaternion.Inverse(a) * b == b`. What is that really saying? Rotate by `a`, but wait actually no undo that, then rotate by `b`.

It's silly. But what if we wanted to calculate where `b` should be after `a` has changed, where we want to retain the same rotational offset between the two. Well, now we are looking at `changedA * Quaternion.Inverse(a) * b == destinationB`. And what is that saying? Rotate by our new rotation of `a`, undo the original rotation of `a`, then rotate by the original rotation of `b`. I am saying "original rotation of `a`/`b`" because in our actual logic we would calculate `rotationFromAToB` initially, and then do `changedA * rotationFromAToB`. In other words, `rotationFromAToB` captured the original rotations, and with that the original offset.

With that capturing in mind, it is no longer quite so silly. Now to come full circle, here's a code snippet:

```cs
Transform a;
Transform b;
Quaternion changedRotation;

// Calculate initial rotational offset from a to b.
Quaternion rotationFromAToB = Quaternion.Inverse(a.rotation) * b.rotation;

// Something changes the rotation of a.
a.rotation = changedRotation;

// Change b's rotation such that it has the same relative rotational offset to a as it had initially.
b.rotation = a.rotation * rotationFromAToB;
```

When thinking about that code, you can mentally substitute the `rotationFromAToB` in the final line with how it was calculated initially. Even though `a` and `b`'s rotations have changed in the mean time which makes the following equation functionally incorrect: `b.rotation = a.rotation * Quaternion.Inverse(a.rotation) * b.rotation;`, what it does do is intuitively show whether the order of operations is correct or not.

`b.rotation = a.rotation * Quaternion.Inverse(a.rotation) * b.rotation;` Is what we've looked at before. `a.rotation * Quaternion.Inverse(a.rotation)` cancel out, so `b.rotation = b.rotation`. And if `a.rotation` has changed, it is the first operand, that change in rotation would propagate in world space correctly. This order of operations makes sense.

Say for example the first line was `Quaternion rotationFromAToB = b.rotation * Quaternion.Inverse(a.rotation);` instead.
We would then be looking at `b.rotation = a.rotation * b.rotation * Quaternion.Inverse(a.rotation);`. And that makes no sense.

If the last line was `b.rotation = rotationFromAToB * a.rotation;`, we don't actually have to do any theoretical substitutions. A change in `a.rotation`, which is in world space, would get applied as a rotation local/relative to `rotationFromAToB`, which makes no sense.

And you can think about it exactly like this both while writing code as well as while reading code, and since `a * Quaternion.Inverse(a)` is so obviously silly, with a bit of time and practice this requires surprisingly little mental gymnastics, even though quaternions have the reputation of being obscure.

There's one more beautiful thing about this rotational offset. We've established previously how `parentRotation * localRotation` gets us the world rotation of an object. Look at `a.rotation * rotationFromAToB`. It's the exact same, it's a world rotation followed by a rotation that is local to that former rotation. In other words, by calculating the rotational offset of 2 world space rotations like this, we are effectively calculating what the local rotation of `b` would be relative to `a`. Or rephrase further, if `b` was a child of `a`, its local rotation would be `rotationFromAToB`.
