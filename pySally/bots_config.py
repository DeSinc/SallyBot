# Discord Stuff
##############################################################################################################################################
Bot_Name= "Sally"
activity = "Neural network brain stuff" # set the bot activity
memory_amount = 20
respond_to_everything = True
ignore_mode= False # ignore messages containing bad shit, otherwise just strip the messages 
                   # and feed that to the bot. Messages containing links will always be ignored.
##############################################################################################################################################


# Sally stuff
##############################################################################################################################################
sally_description = 'A 25 year old anime woman smiling, looking into the camera, short hair, brown hair, brown eyes, golden earrings, blushing, '
Image_input_prompt= "[Sally]: Let me describe the picture with 4 adjectives: "
##############################################################################################################################################



# Custom Settings
##############################################################################################################################################
stableDiffUrl= "http://127.0.0.1:7860" # set the stable diff url
oobabooga_Server = "127.0.0.1"
file_endings = [".png", ".jpg", ".jpeg", ".webp" ]
dalai_is_used= False # use dalai instead of oobabooga
oobabooga_is_used= True # use oobabooga instead of dalai (must be True since dalai is not implemented yet)
stable_diff_is_used= False # use stable diff to make images (on or off, no alternative)
ping_regex_pattern = r"<@(\d+)>"
##############################################################################################################################################



# Misc Settings
##############################################################################################################################################
word_things = ["an", "a", "of"] # it is important that an is before a
banned_words_nsfw= ["naked", "boobs", "boob", "boobies", "pussy", "butt", "bum", "booty", "nuditity"] #ban nsfw words

banned_words_links= ["http", ".com", ".co", ".de", ".ru", ".xyz", "://", ".us", ".net", ".org", ".eu", ".edu", "dotcom", "dotco", 
                     "dotde", "dotru", "dotxyz", "dotus", "dotnet", "dotorg", "doteu", "dotedu"] #ban linkie thing

take_pic_keyword_take= ["take", "paint", "generate", "make", "draw", "create", "show", "give", "snap", "capture", "send", 
                        "display", "share", "shoot", "see", "provide", "another", "print", "illustrate"] 
                        # check if the user want's a picture 1/2
                        
take_pic_keyword_image= ["image", "picture", "painting", "pic", "photo", "portrait", "selfie"] 
                        # check if the user want's a picture 2/2. Will also only send an image if both requirements are met.
##############################################################################################################################################



# oobabooga params
##############################################################################################################################################
params_oobabooga = {
    'prompt': "",
    'max_new_tokens': 200,
    'do_sample': True,
    'temperature': 0.7, # how random she will be; higher = more random
    'top_p': 0.1,        # the bigger the more random?
    'typical_p': 1,      # originality?
    'repetition_penalty': 1.18, # higer = less repetition
    'encoder_repetition_penalty': 1, # higher = less on drugs and more in context? .9 - 1.4?
    'top_k': 40,         # also does the random? 0 - 200
    'no_repeat_ngram_size': 0,
    'num_beams': 1,
    'penalty_alpha': 0,
    'length_penalty': 1,
    'early_stopping': False,
    'add_bos_token': True, # remove = more creative bot? // I think this always indicates the start of a sentence so let it stay
    'custom_stopping_strings': ["[","\n[", "\\n[", "]:", "\n#", "##", "###", "000000000000", "1111111111", "0.0.0.0.", "1.1.1.1.", 
                                "2.2.2.2.", "3.3.3.3.", "4.4.4.4.", "5.5.5.5.", "6.6.6.6.", "7.7.7.7.", "8.8.8.8.", "9.9.9.9.", 
                                "22222222222222", "33333333333333", "4444444444444444", "5555555555555", "66666666666666", 
                                "77777777777777", "888888888888888", "999999999999999999", "01010101", "0123456789", "<noinput>", 
                                "<nooutput>", "<picture>" ],
} # python server.py --model ozcur_alpaca-native-4bit --wbits 4 --groupsize 128 --extensions api --notebook --listen-port 7862 --xformers
##############################################################################################################################################


# stable diff params
##############################################################################################################################################
params_stableDiff = {
   "prompt": "",
    "negative_prompt": "(worst quality, low quality:1.4), 3d, cgi, 3d render, naked, nude, hands, extra fingers, not enough fingers, 6 fingers, 4 fingers",
    "steps": 55,
    "width": 450,
    "height": 400,
    "cfg_scale": 7.5,
    "send_images": True,
    "sampler_name": "DPM++ SDE Karras"
}
##############################################################################################################################################
