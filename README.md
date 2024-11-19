# FindTextFromVideo
Commandline tool to search video file for text using OCR!

### Why?
I recorded +1hr clip that had some personal info in few frames, but cannot find where it was.. So, i think its easier to write tool to search text from video, that trying to find those frames by scrubbing video. Lets see?

### TODO
Still have things todo https://github.com/unitycoder/FindTextFromVideo/issues/2

### Installation
- Clone repo
- Open project in Visual Studio
- Download Tesseract language file from https://github.com/tesseract-ocr/tessdata
- If you use English, then take: https://github.com/tesseract-ocr/tessdata/raw/refs/heads/main/eng.traineddata
- Place data file in the build output folder, under: tessdata/ next to where your FindTextFromVideo.exe file is
- Build it
- Use from commandline

### Usage
- FindTextFromVideo.exe "text to search" videofile.mkv
- Press ctrl+c to stop

### Image
![image](https://github.com/user-attachments/assets/15ec6157-1fc3-47cb-84ff-e5f43a6f08c1)
