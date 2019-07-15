import json
import os
from distutils.dir_util import copy_tree
from shutil import copyfile
import re
import requests

class sample_metadata:
    def reset_props(self):
        self.formal_name = ""
        self.friendly_name = ""
        self.sample_unique_id = ""
        self.category = ""
        self.nuget_packages = []
        self.keywords = []
        self.relevant_api = []
        self.since = ""
        self.images = []
        self.source_files = []
        self.redirect_from = []
        self.offline_data = []
        self.description = ""
        self.how_to_use = []
        self.how_it_works = ""
        self.use_case = ""
        self.data_statement = ""
        self.additional_info = ""
        self.ignore = False

    def __init__(self):
        self.reset_props()
    
    def populate_from_folder(self, folder_path):
        # check for readme in folder

        # check for json in folder


        return
    
    def populate_from_json(self, path_to_json):
        # formal name is the name of the folder containing the json
        pathparts = os.path.split(path_to_json)
        self.formal_name = pathparts[-2]

        # open json file
        with open(path_to_json, 'r') as json_file:
            data = json.load(json_file)
            self.friendly_name = data["title"]
            self.sample_unique_id = data["sample_unique_id"]
            # note: category can also be derived from folder structure
            self.category = data["category"]
            self.nuget_packages = data["packages"]
            self.keywords = data["keywords"]
            self.relevant_api = data["relevant_apis"]
            self.images = data["images"]
            self.source_files = data["snippets"]
            self.redirect_from = data["redirect_from"]
            self.description = data["description"]
            self.ignore = data["ignore"]

        return
    
    def populate_from_readme(self, platform, path_to_readme):
        # formal name is the name of the folder containing the json
        pathparts = sample_metadata.splitall(path_to_readme)
        self.formal_name = pathparts[-2]

        # populate redirect_from; it is based on a pattern
        redirect_string = f"/net/latest/{platform.lower()}/sample-code/{self.formal_name.lower()}.htm"
        self.redirect_from.append(redirect_string)

        # category is the name of the folder containing the sample folder
        self.category = pathparts[-3]

        # read the readme content into a string
        readme_contents = ""
        try:
            readme_file = open(path_to_readme, "r")
            readme_contents = readme_file.read()
            readme_file.close()
        except Exception as err:
            # not a sample, skip
            print(f"Error populating sample from readme - {path_to_readme} - {err}")
            return

        # break into sections
        readme_parts = readme_contents.split("\n\n") # a blank line is two newlines

        # extract human-readable name
        title_line = readme_parts[0].strip()
        if not title_line.startswith("#"):
            title_line = title_line.split("#")[1]
        self.friendly_name = title_line.strip("#").strip()
        
        if len(readme_parts) < 3:
            # can't handle this, return early
            return
        if len(readme_parts) < 5: # old style readme
            # Take just the first description paragraph
            self.description = readme_parts[1]
            self.images = sample_metadata.extract_image_from_image_string(readme_parts[2])
            return
        else:
            self.description = readme_parts[1]
            self.images = sample_metadata.extract_image_from_image_string(readme_parts[2])

            # Read through and add the rest of the sections
            examined_readme_part_index = 2
            current_heading = ""
            para_part_accumulator = []

            while examined_readme_part_index < len(readme_parts):
                current_part = readme_parts[examined_readme_part_index]
                examined_readme_part_index += 1
                if not current_part.startswith("#"):
                    para_part_accumulator.append(current_part)                    
                    continue
                else:
                    # process existing heading, skipping if nothing to add
                    if len(para_part_accumulator) != 0:
                        self.populate_heading(current_heading, para_part_accumulator)
                    # get started with new heading
                    current_heading = current_part
                    para_part_accumulator = []
            # do the last segment
            if current_heading != "" and len(para_part_accumulator) > 0:
                self.populate_heading(current_heading, para_part_accumulator)

        return
    
    def try_replace_with_common_readme(self, platform, path_to_common_dir, path_to_net_readme):
        '''
        Will read the common readme and replace the sample's readme if found wanting
        path_to_common_dir is the path to the samples design folder
        Precondition: populate_from_readme already called
        '''
        # skip if the existing readme is good enough; it is assumed that any sample with tags already has a good readme
        if len(self.keywords) > 0:
            return
        
        # determine if matching readme exists; if not, return early
        match_name = None
        dirs = os.listdir(path_to_common_dir)
        for dir in dirs:
            if dir.lower() == self.formal_name.lower():
                match_name = dir
        if match_name == None:
            return
        
        # create a new sample_metadata, call populate from readme on the design readme
        readme_path = os.path.join(path_to_common_dir, match_name, "readme.md")
        if not os.path.exists(readme_path):
            return
        compare_sample = sample_metadata()
        compare_sample.populate_from_readme(platform, readme_path)

        # fix the image content
        compare_sample.images = [f"{compare_sample.formal_name}.jpg"]

        # fix the category
        compare_sample.category = self.category

        # call flush_to_readme on the newly created sample object
        compare_sample.flush_to_readme(path_to_net_readme)

        # re-read to pick up any new info
        self.reset_props()
        self.populate_from_readme(platform, path_to_net_readme)

    def flush_to_readme(self, path_to_readme):
        template_text = f"# {self.friendly_name}\n\n"

        # add the description
        if self.description != "":
            template_text += f"{self.description}\n\n"

        # add the image
        if len(self.images) > 0:
            template_text += f"![screenshot]({self.images[0]})\n\n"

        # add "Use case" - use_case
        if self.use_case != "":
            template_text += "## Use case\n\n"
            template_text += f"{self.use_case}\n\n"

        # add 'How to use the sample' - how_to_use
        if self.how_to_use != "" and len(self.how_to_use) > 0:
            template_text += "## How to use the sample\n\n"
            template_text += f"{self.how_to_use}\n\n"

        # add 'How it works' - how_it_works
        if len(self.how_it_works) > 0:
            template_text += "## How it works\n\n"
            stepIndex = 1
            for step in self.how_it_works:
                if not step.startswith("***"): # numbered steps
                    template_text += f"{stepIndex}. {step}\n"
                    stepIndex += 1
                else: # sub-bullets
                    template_text += f"    * {step.strip('***')}\n"
            template_text += "\n"

        # add 'Relevant API' - relevant_api
        if len(self.relevant_api) > 0:
            template_text += "## Relevant API\n\n"
            for api in self.relevant_api:
                template_text += f"* {api}\n"
            template_text += "\n"

        # add 'Offline data' - offline_data
        if len(self.offline_data) > 0:
            template_text += "## Offline data\n\n"
            template_text += "This sample downloads the following items from ArcGIS Online automatically:\n\n"
            for item in self.offline_data:
                # get the item's name from AGOL
                request_url = f"https://www.arcgis.com/sharing/rest/content/items/{item}?f=json"
                agol_result = requests.get(url=request_url)
                data = agol_result.json()
                name = data["name"]
                # write out line
                template_text += f"* [{name}](https://www.arcgis.com/home/item.html?id={item}) - {data['snippet']}\n"
            template_text += "\n"

        # add 'About the data' - data_statement
        if self.data_statement != "":
            template_text += "## About the data\n\n"
            template_text += f"{self.data_statement}\n\n"

        # add 'Additional information' - additional_info
        if self.additional_info != "":
            template_text += "## Additional information\n\n"
            template_text += f"{self.additional_info}\n\n"

        # add 'Tags' - keywords
        template_text += "## Tags\n\n"
        template_text += ", ".join(self.keywords)
        template_text += "\n"

        # write the output
        with open(path_to_readme, 'w+') as file:
            file.write(template_text)
        return
    
    def flush_to_json(self, path_to_json):

        data = {}

        data["title"] = self.friendly_name
        data["sample_unique_id"] = self.sample_unique_id
        data["category"] = self.category
        data["keywords"] = self.keywords
        data["relevant_apis"] = self.relevant_api
        data["images"] = self.images
        data["snippets"] = self.source_files
        data["redirect_from"] = self.redirect_from
        data["description"] = self.description
        data["ignore"] = self.ignore

        with open(path_to_json, 'w+') as json_file:
            json.dump(data, json_file, indent=4, sort_keys=True)

        return
    
    def emit_standalone_solution(self, platform, sample_dir, output_root, shared_project_path):
        '''
        Produces a standalone sample solution for the given sample
        platform: one of: Android, iOS, UWP, WPF, XFA, XFI, XFU
        output_root: output folder; should not be specific to the platform
        sample_dir: path to the folder containing the sample's code
        '''
        # create output dir
        output_dir = os.path.join(output_root, platform, self.formal_name)

        if not os.path.exists(output_dir):
            os.makedirs(output_dir)

        # copy template files over - find files in template
        script_dir = os.path.split(os.path.realpath(__file__))[0]
        template_dir = os.path.join(script_dir, "templates", "solutions", platform)
        copy_tree(template_dir, output_dir)
        
        # copy sample files over
        copy_tree(sample_dir, output_dir)

        # copy any out-of-dir files over (e.g. Android layouts, download manager)
        if len(self.source_files) > 0:
            for file in self.source_files:
                if ".." in file:
                    source_path = os.path.join(sample_dir, file)
                    dest_path = os.path.join(output_dir, os.path.split(file)[1])
                    copyfile(source_path, dest_path)

        # TODO - emit sampleName.sample metadata xml file

        # accumulate list of source, xaml, axml, and resource files

        # generate list of replacements
        replacements = {}
        replacements["$$project$$"] = self.formal_name
        replacements["$$embedded_resources$$"] = "" # TODO
        replacements["$$source_files$$"] = "" # TODO
        replacements["$$xaml_files$$"] = "" # TODO
        replacements["$$axml_files$$"] = "" # TODO

        # rewrite files in output - replace template fields
        sample_metadata.rewrite_files_in_place(output_dir, replacements)
        return
    
    def rewrite_files_in_place(source_dir, replacements_dict):
        for r, d, f in os.walk(source_dir):
            for sample_dir in d:
                sample_metadata.rewrite_files_in_place(os.path.join(r, sample_dir), replacements_dict)
            for sample_file_name in f:
                sample_file = os.path.join(r, sample_file_name)
                extension = os.path.splitext(sample_file)[1]
                if extension in [".cs", ".xaml", ".sln", ".md", ".csproj", ".shproj", ".axml"]:
                    # open file, read into string
                    original_contents = ""
                    try:
                        with open(sample_file, "r") as handle:
                            original_contents = handle.read()
                    except UnicodeDecodeError:
                        try:
                            with open(sample_file, "r", encoding='utf-8') as handle:
                                original_contents = handle.read()
                        except UnicodeDecodeError:
                            with open(sample_file, "r", encoding='utf-16') as handle:
                                original_contents = handle.read()
                    # make replacements
                    new_content = original_contents
                    for tag in replacements_dict.keys():
                        new_content = new_content.replace(tag, replacements_dict[tag])
                    # write out new file
                    if new_content != original_contents:
                        os.remove(sample_file)
                        try:
                            with open(sample_file, 'w') as rewrite_handle:
                                rewrite_handle.write(new_content)
                        except UnicodeEncodeError:
                            try:
                                with open(sample_file, 'w', encoding="utf-8") as rewrite_handle:
                                    rewrite_handle.write(new_content)
                            except UnicodeEncodeError:
                                with open(sample_file, 'w', encoding="utf-16") as rewrite_handle:
                                    rewrite_handle.write(new_content)
    
    def splitall(path):
        ## Credits: taken verbatim from https://www.oreilly.com/library/view/python-cookbook/0596001673/ch04s16.html
        allparts = []
        while 1:
            parts = os.path.split(path)
            if parts[0] == path:  # sentinel for absolute paths
                allparts.insert(0, parts[0])
                break
            elif parts[1] == path: # sentinel for relative paths
                allparts.insert(0, parts[1])
                break
            else:
                path = parts[0]
                allparts.insert(0, parts[1])
        return allparts
    
    def extract_image_from_image_string(image_string) -> str:
        '''
        Takes an image string in the form of ![alt-text](path_toImage.jpg)
        or <img src="path_toImage.jpg" width="350"/>
        and returns 'path_toImage.jpg'
        '''

        image_string = image_string.strip()

        if image_string.startswith("!"): # Markdown-style string
            # find index of last )
            close_char_index = image_string.rfind(")")

            # find index of last (
            open_char_index = image_string.rfind("(")

            # return original string if it can't be processed further
            if close_char_index == -1 or open_char_index == -1:
                return image_string

            # read between those chars
            substring = image_string[open_char_index + 1:close_char_index]
            return substring
        else: # HTML-style string
            # find index of src="
            open_match_string = "src=\""
            open_char_index = image_string.rfind(open_match_string)
            
            # return original string if can't be processed further
            if open_char_index == -1:
                return image_string

            # adjust open_char_index to account for search string
            open_char_index += len(open_match_string)
            
            # read from after " to next "
            close_char_index = image_string.find("\"", open_char_index)
            
            # read between those chars
            substring = image_string[open_char_index:close_char_index]
            return substring
    
    def populate_heading(self, heading_part, body_parts):
        '''
        param: heading_part - string starting with ##, e.g. 'Use case'
        param: body_parts - list of constituent strings
        output: determines which field the content belongs in and adds appropriately
                e.g. lists will be turned into python list instead of string
        '''

        # normalize string for easier decisions
        heading_parts = heading_part.strip("#").strip().lower().split()

        # use case
        if "use" in heading_parts and "case" in heading_parts:
            content = "\n\n".join(body_parts)
            self.use_case = content
            return

        # how to use
        if "use" in heading_parts and "how" in heading_parts:
            content = "\n\n".join(body_parts)
            self.how_to_use = content
            return

        # how it works
        if "works" in heading_parts and "how" in heading_parts:
            step_strings = []
            lines = body_parts[0].split("\n")
            cleaned_lines = []
            for line in lines:
                if not line.strip().startswith("*"): # numbered steps
                    line_parts = line.split('.')
                    cleaned_lines.append(".".join(line_parts[1:]).strip())
                else: # sub-bullets
                    cleaned_line = line.strip().strip("*").strip()
                    cleaned_lines.append(f"***{cleaned_line}")
            self.how_it_works = cleaned_lines
            return
        
        # relevant API
        if "api" in heading_parts or "apis" in heading_parts:
            api_strings = []
            lines = body_parts[0].split("\n")
            cleaned_lines = []
            for line in lines:
                # removes nonsense formatting and controls for Qt content sneaking in
                cleaned_lines.append(line.strip("*").strip("-").strip("`").strip().strip("`").replace("::", "."))
            cleaned_lines.sort()
            self.relevant_api = cleaned_lines
            return

        # offline data
        if "offline" in heading_parts:
            content = "\n".join(body_parts)
            # extract any guids - these are AGOL items
            regex = re.compile('[0-9a-f]{8}[0-9a-f]{4}[1-5][0-9a-f]{3}[89ab][0-9a-f]{3}[0-9a-f]{12}', re.I)
            matches = re.findall(regex, content)
            self.offline_data = matches
            return

        # about the data
        if "data" in heading_parts and "about" in heading_parts:
            content = "\n\n".join(body_parts)
            self.data_statement = content
            return

        # additional info
        if "additional" in heading_parts:
            content = "\n\n".join(body_parts)
            self.additional_info = content
            return

        # tags
        if "tags" in heading_parts:
            tags = body_parts[0].split(",")
            cleaned_tags = []
            for tag in tags:
                cleaned_tags.append(tag.strip())
            cleaned_tags.sort()
            self.keywords = cleaned_tags
            return