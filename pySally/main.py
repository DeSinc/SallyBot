import discord 
import pytesseract
import requests
import re 
import datetime 
import json 
import pytz
from PIL import Image
import base64
import io
from bots_config import *



client = discord.Client(intents=discord.Intents.all())
history = []
load_dc_messages = False 



def build_prompt_on_history(history): # build prompt string from the history list
    prompt = ''
    for msg in history:
        if len(msg) == 2:
            prompt = prompt + f'[{msg[0]}]: {msg[1]}\n'
        elif len(msg) == 4:
            prompt = prompt + f'*[{msg[0]}]: {msg[1]}* [{msg[2]}]: {msg[3]}\n'
    return str(prompt)




def check_for_image_request(msg): # check if there are image request keywords in the message
    for i in take_pic_keyword_take:
        if i in msg.lower():
            for j in take_pic_keyword_image:
                if j in msg.lower():
                    print("found image request! Keywords:" + i + " " + j)
                    return True
    return False




def check_for_links(msg): # check if stuff like dotcom or .com is anywhere in the msg string
    for i in banned_words_links:
        found = msg.find(i)
        if found != -1:
            print(f'found {i} in msg! Ignoring this message..')
            return True
    return False




def check_for_profanity(msg): # check for nsfw keywords in the text (might need to improve this)
    for i in banned_words_nsfw:
        if i in msg.lower():
            print(f'found {i} in msg! Ignoring this message..')
            return True
    return False




def clean_user_prompt(msg): # do smart big brain stuff to do things (remove image keywords based on common ways to ask for an image)
    msg = msg.lower()
    for i in take_pic_keyword_take:
        for j in take_pic_keyword_image:
            for k in word_things:
                if f'{i} {k} {j} of a' in msg:                    
                    msg = msg.replace(f'{i} {k} {j} of a' , "") 
                elif f'{i} {k} {j} of an' in msg:
                    msg = msg.replace(f'{i} {k} {j} of an', "") 
                elif f'{i} {k} {j} of' in msg:
                    msg = msg.replace(f'{i} {k} {j} of', "")        # <- this code goes brrrrrrr
                elif f'{i} {k} {j} in a' in msg:                    
                    msg = msg.replace(f'{i} {k} {j} in a' , "") 
                elif f'{i} {k} {j} in an' in msg:
                    msg = msg.replace(f'{i} {k} {j} in an', "")
                

    for i in take_pic_keyword_take:
        for k in word_things:
            for j in take_pic_keyword_image:            # if anything is left try to clean that up aswell
                if j in msg.lower():
                    msg = msg.replace(f'{j}', "")
                    msg = msg.replace(f'{i} {k}', "")
    
    return msg




def cleaner(msg): # replace all the nsfw keywords with the word "cat" (seemed like a good idea to me)
    for word in banned_words_nsfw:
        if word in msg.lower():
            print(f"found {word} in msg! Replacing with cat..")
            msg = msg.replace(word, "cat")
    return msg




def filter_message(msg): # filter these thingys because DeSinc said those are bad to feed into the bot
    msg = msg.replace("{", "")                    
    msg = msg.replace("}", "")
    msg = msg.replace("\"", "'")
    msg = msg.replace("“", "'")
    msg = msg.replace("”", "'")
    msg = msg.replace("’", "'")
    msg = msg.replace("`", "\\`")
    msg = msg.replace("$", "")
    msg = msg.replace(f"{client.user.name}", "sally")
    msg = msg.replace(f'{client.user.name}', "sally")
    return msg




def get_daytime(): # get the current time in japan and return the time as a keyword for the SD prompt (I changed them up a bit)
    b = datetime.datetime.now(tz=pytz.timezone('Asia/Tokyo')).strftime("%H")
    if int(b) >= 6 and int(b) < 12:
        return "sunrise, "
    elif int(b) >= 12 and int(b) < 18:
        return "daytime, "
    elif int(b) >= 18 and int(b) < 24:
        return "sunset, "
    elif int(b) >= 0 and int(b) < 6:
        return "night time, "
    



def oobabooga_modified(msg): # modified version of oobabooga() that only generates 7 tokens instead of 150 (for the SD prompt)
    params_oobabooga["prompt"] = msg
    params_oobabooga["max_new_tokens"] = 7
    payload = json.dumps(params_oobabooga, ensure_ascii=True)
    response = requests.post(f"http://{oobabooga_Server}:5000/api/v1/generate", data=payload)
    response = response.json()
    reply = response["results"][0]["text"]
    print(f"oobabooga reply: {reply}")
    return reply




def oobabooga(msg): # send the prompt to oobabooga and return the generated text
    params_oobabooga["prompt"] = msg
    params_oobabooga["max_new_tokens"] = 150
    payload = json.dumps(params_oobabooga, ensure_ascii=True)
    response = requests.post(f"http://{oobabooga_Server}:5000/api/v1/generate", data=payload)
    response = response.json()
    reply = response["results"][0]["text"]
    print(f"oobabooga reply: {reply}")
    return reply




def ping_to_username(msg): # resolve all the pings inside of a message to their corresponding username
    matches = re.findall(ping_regex_pattern, msg)
    for match in matches:
        user = client.get_user(int(match))
        msg = msg.replace(f"<@{match}>", f"{user.name}")
    return msg




def print_config(): # print the current config for the startup (not many options since I only have ooba integrated so far)
    if oobabooga_is_used:
        print("LLM Selected: oobabooga")
    if dalai_is_used:
        print("LLM Selected: dalai")
    if stable_diff_is_used:
        print("Image generator: stable diffusion")
    else:
        print("No image generator used.")
    if ignore_mode:
        print("Ignoring mode: ON")
    else:
        print("Ignoring mode: OFF")




def stable_diff(stable_prompt, msg): # send the prompt to stable diffusion and save the generated image
    print("Stable Diffusion image requested:")
    payload = params_stableDiff
    time = get_daytime()
    user_msg = filter_message(cleaner(clean_user_prompt(msg)))
    full_prompt = re.sub('\s+', ' ',sally_description + time + user_msg + stable_prompt).strip()
    print("Prompt" + full_prompt)
    payload["prompt"] = full_prompt
    response = requests.post(f"http://127.0.0.1:7860/sdapi/v1/txt2img", json=payload)
    response = response.json()
    image =  response['images']
    for i in image:
        image = Image.open(io.BytesIO(base64.b64decode(i.split(",",1)[0])))
        image.save('selfie.png')






@client.event # start the funny bot stuff (LETS GOOOO)
async def on_ready():
    print("Bot connected to discord")
    await client.change_presence(activity=discord.Game(activity))
    print_config()




@client.event # all of the funny stuff that happens as soon as the bot detects a message is been sent
async def on_message(message):
    global load_dc_messages


    if message.author == client.user or message.author.id in user_blacklist: # if the message is from the bot or the user is blacklisted,
                                                                             # then just completely ignore the message
        return


    elif 'sally' in message.content.lower() or client.user.mentioned_in(message): # if the bot is mentioned in the message, then

        if load_dc_messages: # if discord messages are loaded already

            try:           # try to add the message to the history

                history.reverse()
                history.pop()
                history.pop()        # this is a bit of a hacky way to do it, but it works
                history.reverse()    # so if you ask me why it works?: ???????
                msg = [message async for message in message.channel.history(limit=2)]
                msg.reverse()  

                for i in msg:

                    if message.reference:

                        replied_msg = await message.channel.fetch_message(message.reference.message_id)
                        history.append([replied_msg.author.name, replied_msg.content, i.author.name, i.content]) # append the message which 
                                                                                                                 # is a reply to another message
                    else:

                        history.append([i.author.name, i.content]) # just append the normal message
            except:

                print("No messages in channel yet, skipping and adding artificial message") # I- no clue
                history.append(["random_user", "hello sally, what's up?"])


        if not load_dc_messages: # if the discord messages aren't loaded yet, then load them

            messages = [message async for message in message.channel.history(limit=memory_amount)] # memory amount is the amount of messages to load from

            for i in messages:


                if i.type == discord.MessageType.reply: # if the message is a reply to another message, then append the message and the message it's replying to

                    replied_msg = await message.channel.fetch_message(i.reference.message_id)
                    history.append([replied_msg.author.name, replied_msg.content, i.author.name, i.content])
                

                else:

                    history.append([i.author.name, i.content]) # just append the normal message
            

            history.reverse()
            load_dc_messages = True # ???


        if ignore_mode: # ignore mode ignores all messages that contain profanity or links (but links will always be ignored)


            if check_for_profanity(message.content):

                print(f"{message.author} said some sus shit. Lonely mf, ignored.")
                return

            elif check_for_links(message.content):

                print(f"{message.author} tried to send a link. GET GHOSTED")
                return

            elif message.attachments: # if the message has an attachment


                for ending in file_endings: # if the message attachment ends with an image ending

                    if message.attachments[0].filename.endswith(ending): # only take the first image in the list (I ain't readin all that)


                        img_data = requests.get(message.attachments[0].url).content # download the image from the link
                        with open('downloaded.png', 'wb') as f:
                            f.write(img_data)
                        f.close()
                        print("image downloaded")
                        image_text = (pytesseract.image_to_string(Image.open('downloaded.png'))) # OCR OOOO CRAZYYY (reads text in image)


                        if image_text != "": # if the image has text in it, then send it to the LLM like it's a normal message

                            prompt_history = ping_to_username(build_prompt_on_history(history)) 
                            cleaned_history = cleaner(prompt_history)
                            filtered_history = filter_message(cleaned_history) # do the thingys to the history
                            async with message.channel.typing():

                                response = oobabooga(filtered_history + f'*[{message.author.name}]: {image_text}*\n' f'[sally]: ') # add history 4 context + img text

                                try:

                                    index = min(filter(lambda x: x >= 0, [response.find('['), response.find('*')]))
                                    split_char = response[index]
                                    response = response.split(split_char, maxsplit=1) # if the response has a [ or * in it, then split the response at that char
                                    response = response[0]                            # wacky version which I will use until ooba fixes stopping strings

                                except ValueError:

                                    response = response # if there is no [ or * in the response, then just use the response
                                await message.channel.send(content=response, reference=message)
                

            elif check_for_image_request(message.content): # if the text is a request for an image


                if message.author.id not in user_whitelist: # if the user is not whitelisted, then ignore the message

                    print(f"{message.author} tried to request an image, but is not whitelisted. Ignoring.")
                    return
                

                if stable_diff_is_used: # if stable diff is enabled, do the funny

                    prompt_history = ping_to_username(build_prompt_on_history(history))
                    filtered_history = filter_message(prompt_history)

                    async with message.channel.typing():

                        response = oobabooga_modified(filtered_history + Image_input_prompt)

                        try:

                            index = min(filter(lambda x: x >= 0, [response.find('['), response.find('*')]))
                            split_char = response[index]
                            response = response.split(split_char, maxsplit=1)
                            response = response[0]

                        except ValueError:

                            response = response

                        stable_diff(response, message.content)
                        with open("selfie.png", "rb") as f:
                            selfie = discord.File(f, filename="selfie.png")
                        f.close()

                        # to explain this, content is always the message text, file is the attachment and reference is the message that the bot is replying to
                        await message.channel.send(content="Here you go! ^^",file=selfie, reference=message) 

                else:

                    print("stable diff is not enabled, ignoring image request..") # just ignore the message if stable diff is not enabled
                    return

            else:

                prompt_history = ping_to_username(build_prompt_on_history(history))
                filtered_history = filter_message(prompt_history)
                reply_to_msg = ping_to_username(cleaner(filter_message(message.content))) # to add the message ontop of the history in a reply like style
                                                                                          # so that the bot thinks it is replying to the message
                async with message.channel.typing():

                    response = oobabooga(filtered_history + f'*[{message.author.name}]: {reply_to_msg}*\n[sally]: ')

                    try:

                        index = min(filter(lambda x: x >= 0, [response.find('['), response.find('*')]))
                        split_char = response[index]
                        response = response.split(split_char, maxsplit=1) # explained above
                        response = response[0]

                    except ValueError:

                        response = response
                    await message.channel.send(content=response, reference=message) # only send a message if the prompt didn't contain nsfw keywords or links
    
        else: # if ignore mode is not enabled, then just send the cleaned message version to the LLM

            if check_for_links(message.content):
                return # skips as soon as the message contains a link as it will often be only a link or completely useless without it, so just skip those
            
            elif message.attachments: # if there is a message attachment

                for ending in file_endings:

                    if message.attachments[0].filename.endswith(ending):

                        img_data = requests.get(message.attachments[0].url).content

                        with open('downloaded.png', 'wb') as f:
                            f.write(img_data)
                        f.close()                         # all explained above

                        print("image downloaded")
                        image_text = (pytesseract.image_to_string(Image.open('downloaded.png')))


                        if image_text != "":

                            prompt_history = ping_to_username(build_prompt_on_history(history))
                            cleaned_history = cleaner(prompt_history)
                            filtered_history = filter_message(cleaned_history)

                            async with message.channel.typing():

                                response = oobabooga(filtered_history + f'*[{message.author.name}]: {image_text}*\n' f'[sally]: ')

                                try:

                                    index = min(filter(lambda x: x >= 0, [response.find('['), response.find('*')]))
                                    split_char = response[index]
                                    response = response.split(split_char, maxsplit=1)
                                    response = response[0]

                                except ValueError:

                                    response = response
                                await message.channel.send(content=response, reference=message)

                        else: 
                            print("Image got uploaded but didn't contain text, passing..")
            


            elif check_for_image_request(message.content): # explained above..

                if message.author.id not in user_whitelist:

                    print(f"{message.author} tried to request an image, but is not whitelisted. Ignoring.")
                    return
                

                if stable_diff_is_used:

                    prompt_history = ping_to_username(build_prompt_on_history(history))
                    filtered_history = filter_message(prompt_history)

                    async with message.channel.typing():

                        response = oobabooga_modified(filtered_history + Image_input_prompt)

                        try:

                            index = min(filter(lambda x: x >= 0, [response.find('['), response.find('*')]))
                            split_char = response[index]
                            response = response.split(split_char, maxsplit=1)
                            response = response[0]

                        except ValueError:
                            
                            response = response

                        stable_diff(response, message.content)

                        with open("selfie.png", "rb") as f:
                            selfie = discord.File(f, filename="selfie.png")
                        f.close()

                        await message.channel.send(content="Here you go! ^^", file=selfie, reference=message)

                else:
                    print("stable diff is not enabled, ignoring image request..")
                    return
            
            else:
                # normal text resopnse again
                prompt_history = ping_to_username(build_prompt_on_history(history)) # get the history but clean it this time instead of ignoring it if it contains nsfw keywords
                cleaned_history = cleaner(prompt_history)
                reply_to_msg = ping_to_username(cleaner(filter_message(message.content)))
                filtered_history = filter_message(cleaned_history)

                async with message.channel.typing():

                    response = oobabooga(filtered_history + f'*[{message.author.name}]: {reply_to_msg}*\n[sally]: ')

                    try:

                        index = min(filter(lambda x: x >= 0, [response.find('['), response.find('*')]))
                        split_char = response[index]
                        response = response.split(split_char, maxsplit=1)
                        response = response[0]

                    except ValueError:

                        response = response

                    await message.channel.send(content=response, reference=message)
    
    else:
        return # if the message is random ignore it
            

client.run(bot_token) # run the bot
