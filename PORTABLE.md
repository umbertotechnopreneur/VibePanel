# Use Vibe Panel

1. Unzip the file.
2. Open `VibePanel.exe`.
3. Pick Music or Codex, then a theme. Music is called **Multimedia** in the app.

Keep all the files in the same folder. Windows needs WebView2 to show the app.

The window opens centered and leaves room around it. You can resize it: the preview fits the space, and only the theme list scrolls. All links and the VibeWare manifesto logo stay on one line at the bottom.

You can change the font, text size and light/dark colors. The window shows what your screen could look like.

Choose an installed monospace font from the list, or choose **Custom font** to type a name. Adjust **Saturation**, **Hue**, **Brightness** and **Contrast** under the preview. In short windows, open **Colors +**. **Reset** restores the original values. Your settings are saved and apply to the preview and the display. Theme code can override these values.

Turn **Spectrum** off to hide frequency bars in themes that support the option. VU meters stay visible.

To send it to the USB screen, choose the connected display and press **Show on display**. Change themes while it runs. Press **Stop** to end it.

Choose updates every 1 second, 500, 250, 100, 50 or 25 ms. The 25 ms option is aggressive. Choose Poor, Low, Medium or High JPEG quality. Poor uses very strong compression. The screen may update more slowly if drawing or USB takes longer.

For Codex, pick **OpenAI Codex**, then **Prepare Codex plugin**. Finish installing the plugin and reviewing its hooks inside Codex. No extra login is needed. The app shows the last event it received. [More help](plugins/vibepanel-codex/README.md).

**Log errors** saves a small local technical log. It starts off. Use **Open log** to read it.

**Themes run code on your PC. For now, only get them from the [official repository](https://github.com/umbertotechnopreneur/VibePanel). I still need to check security, memory use and speed.**

Music themes can show real frequency bars and stereo VU meters. They analyze Windows playback, not your microphone. No sound is saved or sent anywhere. The bars show the whole output mix, not just one app. The app never changes your volume.

Windows only for now. Hobby project. No warranty.

[What's left to do](cave.md) · [Build it yourself](docs/build.md) · [More about themes](docs/theme-packs.md)
