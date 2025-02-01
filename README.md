# PXL-Api - The place where all backends for creating PXL-Apps live!

First Things First

If you’re looking for neat PXL-App implementations, check out this repository:

https://github.com/CuminAndPotato/PXL-Clock/blob/main/README.md

---

## About the API

-   The APIs are designed to provide developers with simple yet powerful tools for creating animations or other PXL-Art applications.
-   While the current APIs follow a similar principle, this is not a strict requirement—different approaches are possible.
-   The API design is quite flexible, with only a few key constraints:
-   Each API must include a runner.
-   The runner evaluates an app.
-   An app can be an object, a function, or another construct.
-   The app’s output must ultimately be a frame represented as a byte stream:
-   The frame consists of RGB-encoded pixels.
-   The resolution is defined as width × height.
-   The frame rate follows the FPS of the canvas.
-   These frames must be sent via TCP to the receiver (which could be a simulator or a real device).
-   The metadata of the receiver must be fetched initially via HTTP.
-   Check out the existing implementations for the exact contract—it’s really small and easy to follow.

## Simulator

-   For testing and development, you can start the simulator using:
-   The VSCode build task.
-   The bash script in the build folder.

## Help Wanted 🙏

We welcome all contributions — whether it’s new APIs or improvements to the existing ones. If you’re interested, feel free to jump in!

## License

see: [LICENSE.md](./LICENSE.md)
